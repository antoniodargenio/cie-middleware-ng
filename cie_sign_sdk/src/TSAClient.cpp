#include "TSAClient.h"


#include <stdio.h>
#include <string.h>
#include "ASN1/TimeStampResponse.h"
#include "ASN1/TimeStampRequest.h"
#include "ASN1/TimeStampToken.h"
#include "Base64.h"
#include "UUCLogger.h"

USE_LOG;

#ifdef __ANDROID__
long  GetTSAResponse(char* szTsaURL, char* szTsaUsername, char* szTsaPassword, UUCByteArray& request, UUCByteArray& response);
#else
#include "curl/curl.h"
#ifdef _WIN32
#include <windows.h>
#include <wincrypt.h>
#include <openssl/ssl.h>
#include <openssl/x509.h>
#endif
#endif

static size_t WriteCallback(void *contents, size_t size, size_t nmemb, void *userp);

// Limiti di sicurezza per la richiesta alla TSA
static const long TSA_CONNECT_TIMEOUT_SEC = 10;
static const long TSA_TOTAL_TIMEOUT_SEC = 30;
static const size_t TSA_MAX_RESPONSE_SIZE = 1024 * 1024; // 1 MB

#if !defined(__ANDROID__) && defined(_WIN32)
// libcurl (OpenSSL) non ha un bundle di CA: si caricano le CA radice e
// intermedie dall'archivio certificati di Windows, cosi' il certificato TLS
// della TSA viene verificato davvero.
static CURLcode TsaSslCtxCallback(CURL* curl, void* sslctx, void* parm)
{
	(void)curl; (void)parm;
	X509_STORE* store = SSL_CTX_get_cert_store((SSL_CTX*)sslctx);
	if (store == NULL)
		return CURLE_ABORTED_BY_CALLBACK;

	int loaded = 0;
	const char* names[] = { "ROOT", "CA" };
	for (int i = 0; i < 2; i++)
	{
		HCERTSTORE hStore = CertOpenSystemStoreA(0, names[i]);
		if (hStore == NULL)
			continue;
		PCCERT_CONTEXT pCert = NULL;
		while ((pCert = CertEnumCertificatesInStore(hStore, pCert)) != NULL)
		{
			const unsigned char* p = pCert->pbCertEncoded;
			X509* x = d2i_X509(NULL, &p, (long)pCert->cbCertEncoded);
			if (x != NULL)
			{
				if (X509_STORE_add_cert(store, x) == 1)
					loaded++;
				X509_free(x);
			}
		}
		CertCloseStore(hStore, 0);
	}
	return loaded > 0 ? CURLE_OK : CURLE_ABORTED_BY_CALLBACK;
}
#endif

// Copia sicura in un buffer di dimensione fissa (tronca, termina sempre con 0)
static void SafeCopy(char* dest, size_t destSize, const char* src)
{
	if (src == NULL)
	{
		dest[0] = 0;
		return;
	}

	strncpy(dest, src, destSize - 1);
	dest[destSize - 1] = 0;
}

CTSAClient::CTSAClient(void)
{
	m_szTSAUrl[0] = 0;
	m_szTSAUsername[0] = 0;
	m_szTSAPassword[0] = 0;
}

CTSAClient::~CTSAClient(void)
{
}

void CTSAClient::SetTSAUrl(const char* szUrl)
{
	SafeCopy(m_szTSAUrl, sizeof(m_szTSAUrl), szUrl);
}

void CTSAClient::SetCredential(const char* szUsername, const char* szPassword)
{
	SafeCopy(m_szTSAUsername, sizeof(m_szTSAUsername), szUsername);
	SafeCopy(m_szTSAPassword, sizeof(m_szTSAPassword), szPassword);
}

void CTSAClient::SetUsername(const char* szUsername)
{
	SafeCopy(m_szTSAUsername, sizeof(m_szTSAUsername), szUsername);
}

void CTSAClient::SetPassword(const char* szPassword)
{
	SafeCopy(m_szTSAPassword, sizeof(m_szTSAPassword), szPassword);
}

long CTSAClient::GetTimeStampToken(UUCByteArray& digest, const char* szPolicyID, CTimeStampToken** ppTimeStampToken)
{
	CASN1Integer nounce(1);
	CTimeStampRequest request(szSHA256OID, digest, szPolicyID, nounce);
	//CTimeStampRequest request(szSHA1OID, digest, szPolicyID, nounce);

	UUCByteArray tsaRequest;
	request.toByteArray(tsaRequest);

	UUCByteArray tsdata;
	
#ifdef __ANDROID__

	long nRet = GetTSAResponse(m_szTSAUrl, m_szTSAUsername, m_szTSAPassword, tsaRequest, tsdata);
	if(nRet != 0)
		return nRet;
#else

	if(m_szTSAUrl[0] == 0)
		return -1;

	//general initilization
	CURL* ctx = curl_easy_init();
	if(ctx == NULL)
		return -1;

	//set URL
	curl_easy_setopt(ctx, CURLOPT_URL, m_szTSAUrl);

	//set POST method
	curl_easy_setopt(ctx, CURLOPT_POST, 1);

	//give the data you want to post
	curl_easy_setopt(ctx, CURLOPT_POSTFIELDS, tsaRequest.getContent());

	//give the data lenght
	curl_easy_setopt(ctx, CURLOPT_POSTFIELDSIZE, tsaRequest.getLength());

	//show messages for debugging
	//curl_easy_setopt(ctx, CURLOPT_VERBOSE);

	//set the callback function that handle the data return from server
	//if you don't set this, the return data just show up on the screen
	//size_t write_callback(void *ptr, size_t size, size_t nmemb, void *userp)
	curl_easy_setopt(ctx, CURLOPT_WRITEFUNCTION, WriteCallback);

	// we pass our 'chunk' struct to the callback function
	curl_easy_setopt(ctx, CURLOPT_WRITEDATA, (void *)&tsdata);

	// Verifica del certificato TLS della TSA con le CA dell'archivio di Windows
	curl_easy_setopt(ctx, CURLOPT_SSL_VERIFYPEER, 1L);
	curl_easy_setopt(ctx, CURLOPT_SSL_VERIFYHOST, 2L);
#ifdef _WIN32
	curl_easy_setopt(ctx, CURLOPT_SSL_CTX_FUNCTION, TsaSslCtxCallback);
#endif

	// Solo HTTP/HTTPS, anche dopo eventuali redirect (niente file://, ftp:// ecc.)
	curl_easy_setopt(ctx, CURLOPT_PROTOCOLS, (long)(CURLPROTO_HTTP | CURLPROTO_HTTPS));
	curl_easy_setopt(ctx, CURLOPT_REDIR_PROTOCOLS, (long)(CURLPROTO_HTTP | CURLPROTO_HTTPS));
	curl_easy_setopt(ctx, CURLOPT_FOLLOWLOCATION, 1L);
	curl_easy_setopt(ctx, CURLOPT_MAXREDIRS, 3L);

	// Timeout: una TSA che non risponde non deve bloccare la firma
	curl_easy_setopt(ctx, CURLOPT_CONNECTTIMEOUT, TSA_CONNECT_TIMEOUT_SEC);
	curl_easy_setopt(ctx, CURLOPT_TIMEOUT, TSA_TOTAL_TIMEOUT_SEC);
	curl_easy_setopt(ctx, CURLOPT_NOSIGNAL, 1L);


	struct curl_slist *headers = NULL;
	headers = curl_slist_append(headers, "Content-Type: application/timestamp-query");

	//talor the header to application/binary
	if(m_szTSAUsername[0])
	{
		// Le credenziali (Basic) non viaggiano mai in chiaro: solo su https
		if(_strnicmp(m_szTSAUrl, "https://", 8) != 0)
		{
			LOG_ERR((0, "TSAClient", "Credenziali TSA rifiutate: l'URL non e' https"));
			curl_slist_free_all(headers);
			curl_easy_cleanup(ctx);
			return -1;
		}
		curl_easy_setopt(ctx, CURLOPT_HTTPAUTH, CURLAUTH_BASIC);

		curl_easy_setopt(ctx, CURLOPT_USERNAME, m_szTSAUsername);

		curl_easy_setopt(ctx, CURLOPT_PASSWORD, m_szTSAPassword);
	}

	curl_easy_setopt(ctx, CURLOPT_HTTPHEADER, headers);

	//let's do it...
	CURLcode ret = curl_easy_perform(ctx);

	long httpCode = 0;
	if(ret == CURLE_OK)
		curl_easy_getinfo(ctx, CURLINFO_RESPONSE_CODE, &httpCode);

	//clean up (anche in caso di errore)
	curl_slist_free_all(headers);
	curl_easy_cleanup(ctx);

	// Check for errors
	if(ret != CURLE_OK)
	{
		LOG_MSG((0, "HTTPRequest", "error: %x (%s)", ret, curl_easy_strerror(ret)));
		return ret;
	}

	if(httpCode != 200)
	{
		LOG_ERR((0, "TSAClient", "HTTP status non valido: %ld", httpCode));
		return -1;
	}

	if(tsdata.getLength() == 0)
		return -1;

#endif  // __ANDROID__

	try
	{
		CTimeStampResponse tsResponse((BYTE*)tsdata.getContent(), (int)tsdata.getLength());

		CPKIStatusInfo statusInfo(tsResponse.getPKIStatusInfo());

		if(statusInfo.getStatus().getIntValue() == 0)
		{
			*ppTimeStampToken = new CTimeStampToken(tsResponse.getTimeStampToken());
		}
		else
		{
			LOG_ERR((0, "TSACLient", "CPKIStatusInfo error: %x", statusInfo.getStatus().getIntValue()));

			*ppTimeStampToken = NULL;
		}
	}
	catch(...)
	{
		*ppTimeStampToken = NULL;
	}	

	if(*ppTimeStampToken == NULL)
		return -1;

	return 0;
}

static size_t WriteCallback(void *contents, size_t size, size_t nmemb, void *userp)
{
	UUCByteArray* pData = (UUCByteArray*)userp;

	size_t realsize = size * nmemb;

	// Risposta troppo grande: si interrompe il trasferimento
	if(pData->getLength() + realsize > TSA_MAX_RESPONSE_SIZE)
		return 0;

	pData->append((BYTE*)contents, realsize);

	/*
	CTimeStampToken** ppTimeStampToken = (CTimeStampToken**)userp;

	size_t realsize = size * nmemb;

	CTimeStampResponse tsResponse((BYTE*)contents, (int)realsize);

	CPKIStatusInfo statusInfo(tsResponse.getPKIStatusInfo());

	if(statusInfo.getStatus().getIntValue() == 0)
	{
		*ppTimeStampToken = new CTimeStampToken(tsResponse.getTimeStampToken());
	}
	else
	{
		*ppTimeStampToken = NULL;
	}
*/
	return realsize;
}




