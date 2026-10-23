// Readable names for the plugin data IDs found on cards (KKEx keys).
using System.Collections.Generic;

namespace StudioCharaFilter
{
    internal static class ModNames
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "moreAccessories", "More Accessories" },
            { "com.bepis.sideloader.universalautoresolver", "Sideloader (uses zipmod content)" },
            { "marco.authordata", "Author info" },
            { "Marco.SkinEffects", "Skin Effects" },
            { "KKABMPlugin.ABMData", "ABMX (body bone edits)" },
            { "com.deathweasel.bepinex.materialeditor", "Material Editor" },
            { "com.deathweasel.bepinex.hairaccessorycustomizer", "Hair Accessory Customizer" },
            { "KK_PregnancyPlus", "Pregnancy+" },
            { "KK_Pregnancy", "Pregnancy" },
            { "orange.spork.advikplugin", "Advanced IK" },
            { "com.deathweasel.bepinex.pushup", "Push Up" },
            { "KSOX", "Skin Overlays" },
            { "KCOX", "Clothing Overlays" },
            { "com.deathweasel.bepinex.clothingunlocker", "Clothing Unlocker" },
            { "com.deathweasel.bepinex.uncensorselector", "Uncensor Selector" },
            { "Additional_Card_Info", "Additional Card Info" },
            { "org.njaecha.plugins.dbde", "Dynamic Bone Distribution Editor" },
            { "KK_InvisibleBody", "Invisible Body" },
            { "com.deathweasel.bepinex.eyecontrol", "Eye Control" },
            { "Sauceke.SexFaces", "Sex Faces" },
            { "madevil.kk.ass", "Accessory State Sync" },
            { "Accessory_States", "Accessory States" },
            { "AAAAAAAAAAAA_Data", "AAAAAAAAAAAA (accessory parents)" },
            { "com.snw.bepinex.breastphysicscontroller", "Breast Physics Controller" },
            { "com.deathweasel.bepinex.moreoutfits", "More Outfits" },
            { "com.deathweasel.bepinex.dynamicboneeditor", "Dynamic Bone Editor" },
            { "KK_Profile", "Profile text" },
            { "org.njaecha.plugins.editfangs", "Edit Fangs" },
            { "nakay.kk.ClothingBlendShape", "Clothing Blend Shape" },
            { "nakay.kk.ChaAlphaMask", "Alpha Mask" },
            { "LewdCrestX", "Lewd Crest X" },
            { "picolet21.koikatsu.LewdCrest", "Lewd Crest (old)" },
            { "picolet21.koikatsu.EroStatus", "Ero Status" },
            { "MigrationHelper", "Migration Helper" },
            { "madevil.kk.ca", "Character Accessory" },
            { "madevil.kk.AAAPK", "AAAPK (accessory parents)" },
            { "org.njaecha.plugins.objimport", "OBJ Import" },
            { "org.njaecha.plugins.assetimport", "Asset Import" },
            { "com.deathweasel.bepinex.colliders", "Colliders" },
            { "com.deathweasel.bepinex.studiocolliders", "Studio Colliders" },
            { "com.rikkibalboa.bepinex.buttEditor", "Butt Editor" },
            { "com.shallty.blendshapecreator", "BlendShape Creator" },
            { "com.jim60105.kk.charaoverlaysbasedoncoordinate", "Overlays per Outfit" },
            { "marco.becometrap", "Become Trap" },
            { "kokaiinum.KKExpandMaleMaker", "Expand Male Maker" },
            { "BendUrAcc", "Bend Ur Accessories" },
            { "koik.kokyu", "Breathing (Kokyu)" },
            { "PluginListTool", "Plugin List Tool" },
            { "Bulge", "Bulge" },
        };

        public static string Get(string key)
        {
            string name;
            return Names.TryGetValue(key, out name) ? name : key;
        }

        // Plugin IDs are short printable ASCII; anything else is a misread and gets dropped.
        public static bool IsSaneKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 100) return false;
            foreach (char ch in key)
                if (ch < 0x20 || ch > 0x7e) return false;
            return true;
        }
    }
}
