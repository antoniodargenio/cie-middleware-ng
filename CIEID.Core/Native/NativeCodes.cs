using System;

namespace CIEID.Core
{
    /// <summary>Codici di ritorno PKCS#11 (CKR_*) e codici specifici restituiti da CIEPKI-NG.dll.</summary>
    public static class NativeCodes
    {
        public const int CKR_OK = 0x00000000;
        public const int CKR_CANCEL = 0x00000001;
        public const int CKR_TOKEN_NOT_PRESENT = 0x000000E0;
        public const int CKR_TOKEN_NOT_RECOGNIZED = 0x000000E1;
        public const int CKR_DEVICE_ERROR = 0x00000030;
        public const int CKR_GENERAL_ERROR = 0x00000005;
        public const int CKR_PIN_INCORRECT = 0x000000A0;
        public const int CKR_PIN_INVALID = 0x000000A1;
        public const int CKR_PIN_LEN_RANGE = 0x000000A2;
        public const int CKR_PIN_EXPIRED = 0x000000A3;
        public const int CKR_PIN_LOCKED = 0x000000A4;
        public const int CARD_ALREADY_ENABLED = 0x000000F0;
        public const int CARD_PAN_MISMATCH = 0x000000F1;
        public const int CARD_TSA_ERROR = 0x000000F2;
        public const UInt32 INVALID_FILE_TYPE = 0x84000005;

        // Stato di abbinamento di una CIE
        public const int ENROLLED = 1;
        public const int NOT_ENROLLED = 0;
    }
}
