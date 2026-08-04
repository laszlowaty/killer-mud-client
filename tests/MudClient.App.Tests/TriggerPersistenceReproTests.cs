using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

/// <summary>Empirical repro for a user report: "adding or editing a trigger doesn't seem to
/// survive relaunching the client." Simulates two separate client instances (two separate
/// MainWindowViewModel constructions) sharing the same on-disk profile/settings directories.</summary>
public sealed class TriggerPersistenceReproTests
{
    [Fact]
    public async Task AddedTrigger_ProfileSpecific_SurvivesSimulatedRelaunch()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "KillerMudClient_TriggerReproTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var profileService = new ProfileService(directory);
        var settingsService = new AppSettingsService(directory);

        try
        {
            // --- "Client instance 1": create a profile, add a trigger, close. ---
            await using (var vm1 = new MainWindowViewModel(profileService, settingsService))
            {
                vm1.NewProfileName = "TestHero";
                vm1.NewProfileHost = "killer-mud.pl";
                vm1.NewProfilePort = 4004;
                vm1.CreateProfileCommand.Execute(null);

                vm1.NewRuleName = "MojTrigger";
                vm1.NewRuleType = "trigger";
                vm1.NewRulePattern = "Jestes ranny";
                vm1.NewRuleAction = "heal";
                vm1.NewRuleIsGlobal = false;
                vm1.AddRuleCommand.Execute(null);

                Assert.Contains(vm1.AutomationRules, r => r.Name == "MojTrigger");
            }

            // --- "Client instance 2": fresh VM, same directories — simulates relaunching. ---
            await using var vm2 = new MainWindowViewModel(profileService, settingsService);
            vm2.SelectedProfileName = "TestHero";
            vm2.SelectProfileCommand.Execute(null);

            Assert.Contains(vm2.AutomationRules, r => r.Name == "MojTrigger" && r.Pattern == "Jestes ranny");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task EditedTrigger_ProfileSpecific_SurvivesSimulatedRelaunch()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "KillerMudClient_TriggerReproTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var profileService = new ProfileService(directory);
        var settingsService = new AppSettingsService(directory);

        try
        {
            await using (var vm1 = new MainWindowViewModel(profileService, settingsService))
            {
                vm1.NewProfileName = "TestHero";
                vm1.NewProfileHost = "killer-mud.pl";
                vm1.NewProfilePort = 4004;
                vm1.CreateProfileCommand.Execute(null);

                vm1.NewRuleName = "MojTrigger";
                vm1.NewRuleType = "trigger";
                vm1.NewRulePattern = "Jestes ranny";
                vm1.NewRuleAction = "heal";
                vm1.NewRuleIsGlobal = false;
                vm1.AddRuleCommand.Execute(null);

                var added = Assert.Single(vm1.AutomationRules, r => r.Name == "MojTrigger");

                // Edit: populate the form from the existing entry, change the action, resubmit —
                // exactly what the Automaty panel's "Edytuj" button does.
                vm1.EditRuleCommand.Execute(added);
                vm1.NewRuleAction = "cast 'refresh'";
                vm1.AddRuleCommand.Execute(null);

                Assert.Equal("cast 'refresh'", vm1.AutomationRules.Single(r => r.Name == "MojTrigger").Action);
            }

            await using var vm2 = new MainWindowViewModel(profileService, settingsService);
            vm2.SelectedProfileName = "TestHero";
            vm2.SelectProfileCommand.Execute(null);

            var reloaded = Assert.Single(vm2.AutomationRules, r => r.Name == "MojTrigger");
            Assert.Equal("cast 'refresh'", reloaded.Action);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AddedTrigger_Global_SurvivesSimulatedRelaunch()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "KillerMudClient_TriggerReproTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var profileService = new ProfileService(directory);
        var settingsService = new AppSettingsService(directory);

        try
        {
            await using (var vm1 = new MainWindowViewModel(profileService, settingsService))
            {
                vm1.NewRuleName = "GlobalnyTrigger";
                vm1.NewRuleType = "trigger";
                vm1.NewRulePattern = "cos sie stalo";
                vm1.NewRuleAction = "look";
                vm1.NewRuleIsGlobal = true;
                vm1.AddRuleCommand.Execute(null);

                Assert.Contains(vm1.AutomationRules, r => r.Name == "GlobalnyTrigger");
            }

            await using var vm2 = new MainWindowViewModel(profileService, settingsService);

            Assert.Contains(vm2.AutomationRules, r => r.Name == "GlobalnyTrigger");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
