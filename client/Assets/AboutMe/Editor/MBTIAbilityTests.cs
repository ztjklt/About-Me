namespace AboutMe.Editor
{
    // Run the word dispatch contract and the production mechanisms it now exposes.
    public static class MBTIAbilityTests
    {
        public static void Run()
        {
            TraitWordRuleTests.Run();
            PartyFieldTests.Run();
            PartyMovementTests.Run();
            PartyGuardTests.Run();
            PartyControlTests.Run();
            PartyLootTests.Run();
            PartyVirtualTests.Run();
        }
    }
}
