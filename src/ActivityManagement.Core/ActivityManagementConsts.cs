namespace ActivityManagement
{
    public class ActivityManagementConsts
    {
        public const string LocalizationSourceName = "ActivityManagement";

        // Yazılım sürümü — şema: 1.MAJOR.MINOR
        //   MAJOR (ortadaki): yeni özellik/modül eklenince artar (1.9.0 → 1.10.0)
        //   MINOR (sondaki): küçük düzeltme/iyileştirmede artar (1.9.0 → 1.9.1)
        //   İlk sayı (1): komple tasarım/altyapı değişiminde artar (→ 2.0.0)
        public const string AppVersion = "1.9.0";

        public const string ConnectionStringName = "Default";

        public const bool MultiTenancyEnabled = true;

        public const int MaxNameLength = 128;
        public const int MaxDescriptionLength = 2000;
    }
}
