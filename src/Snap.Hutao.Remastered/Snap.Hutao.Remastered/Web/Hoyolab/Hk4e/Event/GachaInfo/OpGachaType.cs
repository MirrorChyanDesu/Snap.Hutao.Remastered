using System;
using System.Collections.Generic;
using System.Text;

namespace Snap.Hutao.Remastered.Web.Hoyolab.Hk4e.Event.GachaInfo;

public enum OpGachaType
{
    [LocalizationKey(nameof(SH.WebUGCGachaConfigTypePermanentWish))]
    UGCStandard = 1000,

    [LocalizationKey(nameof(SH.WebUGCGachaConfigTypeAvatarEventWish))]
    UGCAvatarEventWish = 2000,

    [LocalizationKey(nameof(SH.WebUGCGachaConfigTypeActivityAvatarMaleOne))]
    UGCActivityAvatarMaleOne = 20011,

    [LocalizationKey(nameof(SH.WebUGCGachaConfigTypeActivityAvatarMaleTwo))]
    UGCActivityAvatarMaleTwo = 20012,

    [LocalizationKey(nameof(SH.WebUGCGachaConfigTypeActivityAvatarFemaleOne))]
    UGCActivityAvatarFemaleOne = 20021,

    [LocalizationKey(nameof(SH.WebUGCGachaConfigTypeActivityAvatarFemaleTwo))]
    UGCActivityAvatarFemaleTwo = 20022
}
