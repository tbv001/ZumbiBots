using System;
using System.Linq;

namespace ZumbiBots.Classes;

public static class Randomization
{
    public static Gender GetRandomGender()
    {
        var genderList = Enum.GetValues(typeof(Gender)).Cast<Gender>().ToList();
        return genderList[UnityEngine.Random.Range(0, genderList.Count)];
    }

    public static SkinID GetRandomSkin()
    {
        var availableSkinIDs = new[]
        {
            SkinID.Underwear,
            SkinID.CasualAttire,
            SkinID.Skater,
            SkinID.BusinessSuit,
            SkinID.WinterSurvivor,
            SkinID.Riot,
            SkinID.Queen,
            SkinID.WorkerMiner,
            SkinID.CombatBala,
            SkinID.CombatHat,
            SkinID.Paramedic,
            SkinID.Miner
        };
        return availableSkinIDs[UnityEngine.Random.Range(0, availableSkinIDs.Length)];
    }
}
