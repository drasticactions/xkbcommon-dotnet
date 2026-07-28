namespace Xkb.Native;

// Opaque struct definitions for foreign types the generated bindings reference
// but whose headers are deliberately not traversed during generation. They are
// only ever used behind pointers, so an empty struct is sufficient.

/// <summary>Opaque stand-in for C's <c>FILE</c> (from stdio.h); only used as <c>FILE*</c>.</summary>
public partial struct _IO_FILE
{
}

/// <summary>Opaque stand-in for glibc's <c>va_list</c> element type; only used behind a pointer in log callbacks.</summary>
public partial struct __va_list_tag
{
}

/// <summary>Opaque stand-in for libxcb's <c>xcb_connection_t</c>; only used as <c>xcb_connection_t*</c> by the X11 bindings.</summary>
public partial struct xcb_connection_t
{
}
