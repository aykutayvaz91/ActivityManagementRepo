namespace ActivityManagement
{
    public class ActivityManagementConsts
    {
        public const string LocalizationSourceName = "ActivityManagement";

        // Yazılım sürümü — şema: 1.MAJOR.MINOR  (MINOR .1'den başlar, ".0" KULLANILMAZ)
        //   Her küçük değişiklikte MINOR artar:            1.9.2 → 1.9.3 → 1.9.4 → 1.9.5
        //   5'ten sonra (veya büyük değişiklikte) MAJOR atlanır, MINOR .1'e döner:  1.9.5 → 1.10.1
        //   Komple tasarım/altyapı değişiminde ilk sayı artar:  → 2.1.1
        public const string AppVersion = "1.12.2";

        public const string ConnectionStringName = "Default";

        public const bool MultiTenancyEnabled = true;

        public const int MaxNameLength = 128;
        public const int MaxDescriptionLength = 2000;
    }
}
