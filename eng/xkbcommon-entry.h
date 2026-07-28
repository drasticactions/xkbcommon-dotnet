/* Wrapper TU for ClangSharp generation of the core libxkbcommon API.
 * Pulls in xkbcommon.h (which itself includes xkbcommon-names.h and
 * xkbcommon-keysyms.h) plus xkbcommon-compose.h so the core and compose
 * APIs land in a single generation pass — they live in the same shared
 * library (libxkbcommon.so.0) and the same generated class.
 * xkbcommon-compat.h (deprecated aliases) is deliberately not traversed. */
#include <xkbcommon/xkbcommon.h>
#include <xkbcommon/xkbcommon-compose.h>
