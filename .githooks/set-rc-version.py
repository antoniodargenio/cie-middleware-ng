import re, sys
# Aggiorna FILEVERSION/PRODUCTVERSION e FileVersion/ProductVersion in CSP/res/CIEPKI.rc (UTF-16 LE con BOM, CRLF)
ver = sys.argv[1]
path = "CSP/res/CIEPKI.rc"
raw = open(path, "rb").read()
bom = raw[:2] == b"\xff\xfe"
txt = raw.decode("utf-16")
c = ver.replace(".", ",")
txt = re.sub(r"((?:FILE|PRODUCT)VERSION )[0-9,]+", lambda m: m.group(1) + c, txt)
txt = re.sub(r'(VALUE "(?:File|Product)Version", ")[0-9.]+(")', lambda m: m.group(1) + ver + m.group(2), txt)
out = txt.encode("utf-16-le")
open(path, "wb").write((b"\xff\xfe" if bom else b"") + out)
