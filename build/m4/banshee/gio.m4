AC_DEFUN([BANSHEE_CHECK_GIO_SHARP],
[
	AC_ARG_ENABLE(gio, AC_HELP_STRING([--disable-gio], [Disable GIO for IO operations]), ,enable_gio="yes")
	AC_ARG_ENABLE(gio_hardware, AC_HELP_STRING([--disable-gio-hardware], [Disable GIO Hardware backend]), ,enable_gio_hardware="yes")
	
	if test "x$enable_gio" = "xyes"; then
		PKG_CHECK_MODULES(GIOSHARP,
			gio-sharp-3.0 >= 2.99,
			enable_gio="$enable_gio", enable_gio=no)

		if test "x$enable_gio_hardware" = "xyes"; then
			PKG_CHECK_MODULES(GUDEV_SHARP,
				gudev-sharp-3.0 >= 0.2,
				enable_gio_hardware="$enable_gio", enable_gio_hardware=no)

			if test "x$enable_gio_hardware" = "xno"; then
				GUDEV_SHARP_LIBS=''
			fi
		fi

		AM_CONDITIONAL(ENABLE_GIO, test "x$enable_gio" = "xyes")
		AM_CONDITIONAL(ENABLE_GIO_HARDWARE, test "x$enable_gio_hardware" = "xyes")
	else
		enable_gio_hardware="no"
		AM_CONDITIONAL(ENABLE_GIO, false)
		AM_CONDITIONAL(ENABLE_GIO_HARDWARE, false)
	fi
])

