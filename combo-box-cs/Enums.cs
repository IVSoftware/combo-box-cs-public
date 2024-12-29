using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace combo_box_cs
{
    enum WindowsMessages
    {
        LB_GETSELCOUNT = 0x018F,
        LB_GETCOUNT = 0x0186,
        LB_GETCURSEL = 0x019E,
        LB_GETTEXTLEN = 0x0197,
        LB_GETTEXT = 0x0188,
        LB_SETCURSEL = 0x01AF,
        LB_GETLISTBOXINFO = 0x01AE,
        LB_GETTOPINDEX = 0x018A,
        LB_SETTOPINDEX = 0x0189,
        LB_GETITEMRECT = 0x018B,
        WM_SETTEXT = 0x000C,
        WM_ACTIVATEAPP = 0x001C,
        WM_PAINT = 0x0090,
        WM_DESTROY = 0x0002,
        WM_NCDESTROY = 0x0082,
        WM_SIZE = 0x0005,
        WM_CHANGEUISTATE = 0x01A1,
        WM_WINDOWPOSCHANGING = 0x0046,
        WM_UPDATEUISTATE = 0x01A2,
        WM_SETFONT = 0x0022,
        WM_WINDOWPOSCHANGED = 0x0047,
        WM_MOVE = 0x0003,
        WM_NCCREATE = 0x0083,
        WM_DPICHANGED = 0x0317,
        WM_NEXTDLGCTL = 0x00AF,
        WM_ERASEBKGND = 0x0014,
        WM_GETDPISCALEDSIZE = 0x0318,
        WM_MOUSEMOVE = 0x0200,
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_NCPAINT = 0x0085,
        WM_CAPTURECHANGED = 0x0215,
        WM_SHOWWINDOW = 0x0018,
        WM_NCMOUSEHOVER = 0x02A3,
        CB_SETCURSEL = 0x014E
    }

}
