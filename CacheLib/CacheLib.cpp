#include "stdafx.h"
#include "CacheLib.h"
#include <Shlwapi.h>
#include <Shlobj.h>
#include <stdio.h>
#include <vector>
#include <fstream>
#include "sddl.h"
#include "Aclapi.h"
#include <VersionHelpers.h>

#include "..\CSP\Util\util.h"


/// Il contenuto del file di cache (certificato e prima parte del PIN) arriva GIA' CIFRATO dal chiamante
/// (IAS::SetCache, AES con chiave derivata dalla carta: senza la CIE fisica non e' decifrabile).
/// Questa libreria si limita a memorizzarlo in %PROGRAMDATA%\CIEPKI\<PAN>.cache; il formato del file
/// resta invariato per mantenere la compatibilita' con le cache create da altre versioni.

namespace {
	const uint32_t MAX_CACHE_FILE_SIZE = 64 * 1024;

	// Il PAN diventa parte del nome del file: ammessi solo caratteri alfanumerici (niente path traversal)
	bool IsValidPan(const char *PAN) {
		if (PAN == nullptr)
			return false;
		size_t n = strnlen(PAN, 65);
		if (n == 0 || n > 64)
			return false;
		for (size_t i = 0; i < n; i++)
			if (!isalnum((unsigned char)PAN[i]))
				return false;
		return true;
	}

	// Legge e valida il file di cache: [len][PIN cifrato][len][certificato cifrato]
	bool ReadCacheFile(const char *szPath, std::vector<uint8_t> &pin, std::vector<uint8_t> &cert) {
		std::ifstream file(szPath, std::ios::in | std::ios::binary);
		if (!file)
			return false;

		file.seekg(0, std::ios::end);
		std::streamoff fsize = file.tellg();
		file.seekg(0, std::ios::beg);
		if (fsize < (std::streamoff)(2 * sizeof(uint32_t)) || fsize > (std::streamoff)MAX_CACHE_FILE_SIZE)
			throw logged_error("File di cache non valido");

		std::vector<uint8_t> data((size_t)fsize);
		file.read((char*)data.data(), fsize);
		if (!file)
			throw logged_error("Impossibile leggere il file di cache");

		size_t pos = 0;
		auto readBlock = [&](std::vector<uint8_t> &out) {
			if (data.size() - pos < sizeof(uint32_t))
				throw logged_error("File di cache non valido");
			uint32_t len;
			memcpy(&len, data.data() + pos, sizeof(len));
			pos += sizeof(len);
			if (len > data.size() - pos)
				throw logged_error("File di cache non valido");
			out.assign(data.begin() + pos, data.begin() + pos + len);
			pos += len;
		};

		readBlock(pin);
		readBlock(cert);
		return true;
	}
}

std::string commonData;

std::string GetCardDir() {

	if (commonData.empty()) {
		char szPath[MAX_PATH];
		DWORD n = ExpandEnvironmentStrings("%PROGRAMDATA%\\CIEPKI", szPath, MAX_PATH);
		if (n == 0 || n > MAX_PATH)
			throw logged_error("Impossibile determinare la cartella della cache");
		commonData = szPath;
	}
	return commonData;
}

// Restituisce false se il PAN non e' valido o il percorso supera MAX_PATH
bool GetCardPath(const char *PAN, char szPath[MAX_PATH]) {
	if (!IsValidPan(PAN))
		return false;

	auto Path = GetCardDir();

	if (Path.empty() || Path.back() != '\\')
		Path += '\\';

	Path += std::string(PAN);
	Path += ".cache";
	if (Path.length() >= MAX_PATH)
		return false;
	strcpy_s(szPath, MAX_PATH, Path.c_str());
	return true;
}

bool CacheExists(const char *PAN) {
	char szPath[MAX_PATH];
	if (!GetCardPath(PAN, szPath))
		return false;
	return PathFileExists(szPath);
}

bool CacheRemove(const char *PAN)
{
	char szPath[MAX_PATH];
	if (!GetCardPath(PAN, szPath))
		return false;
	if (PathFileExists(szPath))
	{
		return DeleteFile(szPath);
	}

	return false;
}


void CacheGetCertificate(const char *PAN, std::vector<uint8_t>&certificate)
{
	if (PAN == nullptr)
		throw logged_error("Il PAN è necessario");

	char szPath[MAX_PATH];
	if (!GetCardPath(PAN, szPath))
		throw logged_error("PAN non valido");

	std::vector<uint8_t> pin, cert;
	if (!ReadCacheFile(szPath, pin, cert))
		throw logged_error("CIE non abilitata");

	certificate = std::move(cert);
}

void CacheGetPIN(const char *PAN, std::vector<uint8_t>&PIN) {
	if (PAN == nullptr)
		throw logged_error("Il PAN è necessario");

	char szPath[MAX_PATH];
	if (!GetCardPath(PAN, szPath))
		throw logged_error("PAN non valido");

	std::vector<uint8_t> pin, cert;
	if (!ReadCacheFile(szPath, pin, cert))
		throw logged_error("CIE non abilitata");

	PIN = std::move(pin);
}



void CacheSetData(const char *PAN, uint8_t *certificate, int certificateSize, uint8_t *FirstPIN, int FirstPINSize) {
	if (PAN == nullptr)
		throw logged_error("Il PAN è necessario");

	if (!IsValidPan(PAN))
		throw logged_error("PAN non valido");
	if (certificate == nullptr || certificateSize < 0 || FirstPIN == nullptr || FirstPINSize < 0)
		throw logged_error("Dati di cache non validi");

	auto szDir = GetCardDir();
	if (szDir.length() >= MAX_PATH)
		throw logged_error("Percorso della cache troppo lungo");
	char chDir[MAX_PATH];
	strcpy_s(chDir, szDir.c_str());

	if (!PathFileExists(chDir)) {

		//creo la directory dando l'accesso a Edge (utente Packege).
		//Edge gira in low integrity quindi non potrà scrivere (enrollare) ma solo leggere il certificato
		bool done = false;
		CreateDirectory(chDir, nullptr);

		if (IsWindows8OrGreater()) {
			PACL pOldDACL = nullptr, pNewDACL = nullptr;
			PSECURITY_DESCRIPTOR pSD = nullptr;
			EXPLICIT_ACCESS ea;
			SECURITY_INFORMATION si = DACL_SECURITY_INFORMATION;

			DWORD dwRes = GetNamedSecurityInfo(chDir, SE_FILE_OBJECT, DACL_SECURITY_INFORMATION, NULL, NULL, &pOldDACL, NULL, &pSD);
			if (dwRes != ERROR_SUCCESS)
				throw logged_error("Impossibile attivare la CIE nel processo corrente");

			PSID TheSID = nullptr;
			DWORD SidSize = SECURITY_MAX_SID_SIZE;
			if (!(TheSID = LocalAlloc(LMEM_FIXED, SidSize))) {
				if (pSD != NULL)
					LocalFree((HLOCAL)pSD);
				throw logged_error("Impossibile attivare la CIE nel processo corrente");
			}

			if (!CreateWellKnownSid(WinBuiltinAnyPackageSid, NULL, TheSID, &SidSize)) {
				if (TheSID != NULL)
					LocalFree((HLOCAL)TheSID);
				if (pSD != NULL)
					LocalFree((HLOCAL)pSD);
				throw logged_error("Impossibile attivare la CIE nel processo corrente");
			}

			ZeroMemory(&ea, sizeof(EXPLICIT_ACCESS));
			ea.grfAccessPermissions = GENERIC_READ;
			ea.grfAccessMode = SET_ACCESS;
			ea.grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
			ea.Trustee.TrusteeForm = TRUSTEE_IS_SID;
			ea.Trustee.TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP;
			ea.Trustee.ptstrName = (LPSTR)TheSID;

			if (SetEntriesInAcl(1, &ea, pOldDACL, &pNewDACL) != ERROR_SUCCESS)
			{
				if (TheSID != NULL)
					LocalFree((HLOCAL)TheSID);
				if (pSD != NULL)
					LocalFree((HLOCAL)pSD);
				if (pNewDACL != NULL)
					LocalFree((HLOCAL)pNewDACL);
				throw logged_error("Impossibile attivare la CIE nel processo corrente");
			}

			if (SetNamedSecurityInfo(chDir, SE_FILE_OBJECT, si, NULL, NULL, pNewDACL, NULL) != ERROR_SUCCESS)
			{
				if (pNewDACL != NULL)
					LocalFree((HLOCAL)pNewDACL);
				if (TheSID != NULL)
					LocalFree((HLOCAL)TheSID);
				if (pSD != NULL)
					LocalFree((HLOCAL)pSD);
				throw logged_error("Impossibile attivare la CIE nel processo corrente");
			}

			// rilascio le risorse allocate anche nel percorso di successo
			LocalFree((HLOCAL)pNewDACL);
			LocalFree((HLOCAL)TheSID);
			LocalFree((HLOCAL)pSD);
		}
	}
	char szPath[MAX_PATH];
	if (!GetCardPath(PAN, szPath))
		throw logged_error("PAN non valido");

	ByteArray baCertificate(certificate, certificateSize);
	ByteArray baFirstPIN(FirstPIN, FirstPINSize);

	// Scrittura su file temporaneo e sostituzione atomica: un errore a meta' non lascia una cache corrotta
	std::string tmpPath = std::string(szPath) + ".tmp";
	{
		std::ofstream file(tmpPath, std::ofstream::out | std::ofstream::binary | std::ofstream::trunc);

		uint32_t len = (uint32_t)baFirstPIN.size();
		file.write((char*)&len, sizeof(len));
		file.write((char*)baFirstPIN.data(), len);

		len = (uint32_t)baCertificate.size();
		file.write((char*)&len, sizeof(len));
		file.write((char*)baCertificate.data(), len);
		file.flush();

		if (!file) {
			file.close();
			DeleteFile(tmpPath.c_str());
			throw logged_error("Impossibile scrivere il file di cache");
		}
	}

	if (!MoveFileEx(tmpPath.c_str(), szPath, MOVEFILE_REPLACE_EXISTING)) {
		DeleteFile(tmpPath.c_str());
		throw logged_error("Impossibile salvare il file di cache");
	}
}
