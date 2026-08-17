using System.Text;
using Assets.Failsafe.Scripts.RandomGeneration;
using UnityEditor;
using UnityEngine;

namespace Failsafe.Editor
{
    public static class EngineerGenerationDebugWindow
    {
        private const string MenuPath = "Failsafe/Debug/Generate Engineers";

        [MenuItem(MenuPath)]
        private static void GenerateEngineers()
        {
            var config = Selection.activeObject as EngineerGenerationConfig;

            if (config == null)
            {
                Debug.LogError(
                    "[EngineerGeneration] Select an EngineerGenerationConfig asset first.");
                return;
            }

            var generator = new EngineerBuildGenerator(new RandomGenerator());

            if (!generator.TryGenerateForNewRun(
                    config,
                    out EngineerGenerationResult result,
                    out string error))
            {
                Debug.LogError($"[EngineerGeneration] {error}", config);
                return;
            }

            Debug.Log(CreateReport(result), config);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanGenerateEngineers()
        {
            return Selection.activeObject is EngineerGenerationConfig;
        }

        private static string CreateReport(EngineerGenerationResult result)
        {
            var report = new StringBuilder();
            report.AppendLine($"[EngineerGeneration] Run seed: {result.Seed}");

            for (int engineerIndex = 0;
                 engineerIndex < result.Engineers.Count;
                 engineerIndex++)
            {
                EngineerBuild engineer = result.Engineers[engineerIndex];

                report.AppendLine();
                report.AppendLine(
                    $"{engineerIndex + 1}. " +
                    $"{engineer.OperatorCode} {engineer.Name}");
                report.AppendLine($"Budget: {engineer.TotalWeight}");

                for (int perkIndex = 0;
                     perkIndex < engineer.Perks.Count;
                     perkIndex++)
                {
                    EngineerPerk perk = engineer.Perks[perkIndex];
                    string cost = perk.Cost >= 0
                        ? $"+{perk.Cost}"
                        : perk.Cost.ToString();
                    string type = perk.IsNegative ? "Negative" : "Positive";

                    report.AppendLine(
                        $"  - {perk.Definition.DisplayName} " +
                        $"[{perk.Definition.Id}] | {type} | " +
                        $"{perk.Rarity} | cost {cost}");
                }

                report.AppendLine($"Spent on perks: {engineer.SpentWeight}");
                report.AppendLine(
                    $"Equipment remainder: {engineer.RemainingWeight}");
            }

            return report.ToString();
        }
    }
}
