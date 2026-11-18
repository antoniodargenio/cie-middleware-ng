#pragma once

#include <windows.h>
#include <atlbase.h>
#include <atlhost.h>
#include <atlstr.h>
#include <stdint.h> 
#include "util/defines.h"
#include "util/array.h"
#include "util/log.h"
#include "util/funccallinfo.h"
#include "util/util.h"
#include "util/utilexception.h"
#include "../LOGGER/Logger.h"

#define ODS(s) LOG_DEBUG(s)

extern ByteArray baNXP_ATR;
extern ByteArray baGemalto_ATR;
extern ByteArray baGemalto2_ATR;
extern ByteArray baSTM_ATR;
extern ByteArray baSTM2_ATR;
extern ByteArray baSTM3_ATR;