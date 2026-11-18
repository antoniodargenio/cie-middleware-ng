#include "CIESign.h"
#include "disigonsdk.h"
#include <string.h>


// Firma di documenti con la CIE: costruttore, riceve l'oggetto IAS per la comunicazione con la carta
CIESign::CIESign(IAS *ias){
	this->ias = ias;
}

// Firma un file (type: PAdES per i PDF, CAdES per .p7m) con PIN, firma grafica opzionale (pagina, posizione e
// dimensioni in coordinate relative alla pagina, immagine) e marca temporale opzionale (URL e credenziali TSA).
// Usa le funzioni di firma dell'SDK (disigon_*). Restituisce 0 se riuscita, altrimenti un codice di errore DISIGON_*
// (DISIGON_ERROR_TSA se la marca temporale non e' stata ottenuta; 1000 per eccezioni non gestite).
uint32_t CIESign::sign(const char* inFilePath, const char* type, const char* pin, int page, float x, float y, float w, float h, const char* imagePathFile,const char* outFilePath, const char* tsaUrl, const char* tsaUsername, const char* tsaPassword)
{
	uint32_t response;

	DISIGON_CTX ctx = NULL;
	long ret = 0;

	try
	{
		ctx = disigon_sign_init();

#if 0
		ret = disigon_set(DISIGON_OPT_LOG_LEVEL, (void*)LOG_TYPE_DEBUG);
		if (ret != 0)
		{
			throw ret;
		}
#endif
		ret = disigon_sign_set(ctx, DISIGON_OPT_IAS_INSTANCE, (IAS*)(this->ias));
		if (ret != 0)
		{
			throw ret;
		}
		ret = disigon_sign_set(ctx, DISIGON_OPT_CADES, (void*)1);
		if (ret != 0)
		{
			throw ret;
		}
		ret = disigon_sign_set(ctx, DISIGON_OPT_PIN, (void*)pin);
		if (ret != 0)
		{
			throw ret;
		}

		ret = disigon_sign_set(ctx, DISIGON_OPT_INPUTFILE, (void*)inFilePath);
		if (ret != 0)
		{
			throw ret;
		}

		ret = disigon_sign_set(ctx, DISIGON_OPT_OUTPUTFILE, (void*)outFilePath);
		if (ret != 0)
		{
			throw ret;
		}

		// Marca temporale (RFC 3161) sulla firma: solo URL http/https, lunghezza limitata
		if (tsaUrl != nullptr && tsaUrl[0] != 0)
		{
			if (strnlen(tsaUrl, 256) >= 256 ||
				(_strnicmp(tsaUrl, "http://", 7) != 0 && _strnicmp(tsaUrl, "https://", 8) != 0))
			{
				ret = DISIGON_ERROR_TSA;
				throw ret;
			}

			ret = disigon_sign_set(ctx, DISIGON_OPT_TSA_URL, (void*)tsaUrl);
			if (ret != 0)
			{
				throw ret;
			}

			// Credenziali (Basic auth) opzionali per la TSA
			if (tsaUsername != nullptr && tsaUsername[0] != 0)
			{
				if (strnlen(tsaUsername, 256) >= 256 || (tsaPassword != nullptr && strnlen(tsaPassword, 256) >= 256))
				{
					ret = DISIGON_ERROR_TSA;
					throw ret;
				}

				ret = disigon_sign_set(ctx, DISIGON_OPT_TSA_USERNAME, (void*)tsaUsername);
				if (ret != 0)
				{
					throw ret;
				}

				ret = disigon_sign_set(ctx, DISIGON_OPT_TSA_PASSWORD, (void*)(tsaPassword != nullptr ? tsaPassword : ""));
				if (ret != 0)
				{
					throw ret;
				}
			}
		}

		if (strcmp(type, "pdf") == 0)
		{
			ret = disigon_sign_set(ctx, DISIGON_OPT_PDF_SUBFILTER, (void*)DISIGON_PDF_SUBFILTER_ETSI_CADES);
			if (ret != 0)
			{
				throw ret;
			}

			ret = disigon_sign_set(ctx, DISIGON_OPT_PDF_PAGE, (void*)&page);
			if (ret != 0)
			{
				throw ret;
			}

			ret = disigon_sign_set(ctx, DISIGON_OPT_PDF_LEFT, (void*)&x);
			if (ret != 0)
			{
				throw ret;
			}

			ret = disigon_sign_set(ctx, DISIGON_OPT_PDF_BOTTOM, (void*)&y);
			if (ret != 0)
			{
				throw ret;
			}

			ret = disigon_sign_set(ctx, DISIGON_OPT_PDF_WIDTH, (void*)&w);
			if (ret != 0)
			{
				throw ret;
			}

			ret = disigon_sign_set(ctx, DISIGON_OPT_PDF_HEIGHT, (void*)&h);
			if (ret != 0)
			{
				throw ret;
			}

			if (imagePathFile)
			{
				ret = disigon_sign_set(ctx, DISIGON_OPT_PDF_IMAGEPATH, (void*)imagePathFile);
				if (ret != 0)
				{
					throw ret;
				}
			}

			ret = disigon_sign_set(ctx, DISIGON_OPT_INPUTFILE_TYPE, (void*)DISIGON_FILETYPE_PDF);
			if (ret != 0)
			{
				throw ret;
			}
		}
		else {

			//TODO renderlo case insensitive
			if ((strstr(inFilePath, "p7m") != NULL) || (strstr(inFilePath, "p7s") != NULL))
				ret = disigon_sign_set(ctx, DISIGON_OPT_INPUTFILE_TYPE, (void*)DISIGON_FILETYPE_P7M);
			else
				ret = disigon_sign_set(ctx, DISIGON_OPT_INPUTFILE_TYPE, (void*)DISIGON_FILETYPE_PLAINTEXT);

			if (ret != 0)
			{
				throw ret;
			}

			ret = disigon_sign_set(ctx, DISIGON_OPT_DETACHED, (void*)0);
			if (ret != 0)
			{
				throw ret;
			}
		}

		ret = disigon_sign_set(ctx, DISIGON_OPT_VERIFY_USER_CERTIFICATE, 0);
		if (ret != 0)
		{
			throw ret;
		}


		ret = disigon_sign_sign(ctx);
		if (ret != 0)
		{
			throw ret;
		}
	}
	catch (long err) {
		LOG_ERROR("CIESign::sign - Error 0x%lx", (unsigned long)err);
	}
	catch (...)
	{
		LOG_ERROR("CIESign::sign - eccezione non gestita");
		if (ctx)
			disigon_sign_cleanup(ctx);
		return 1000;
	}

	if (ctx)
		disigon_sign_cleanup(ctx);

	response = (uint32_t)ret;

	return response;

}