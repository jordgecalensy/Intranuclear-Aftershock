using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Failsafe.Inventory.Presentation.Tests
{
    public sealed class RuviEmotionTests
    {
        private const string LibraryPath = "Assets/Failsafe/Inventory/Ruvi/Emotions/RuviEmotions.asset";

        [Test]
        public void LibraryProvidesTheFiveSpecifiedProbabilities()
        {
            RuviEmotionLibrary library = AssetDatabase.LoadAssetAtPath<RuviEmotionLibrary>(LibraryPath);
            Assert.That(library, Is.Not.Null);
            Assert.That(library.NeutralFace, Is.Not.Null);
            Assert.That(library.TotalWeight, Is.EqualTo(100));
            int[] counts = new int[library.Emotions.Count];
            for (int roll = 0; roll < library.TotalWeight; roll++)
            {
                Assert.That(library.TrySelect(roll, out RuviEmotionLibrary.Emotion selected), Is.True);
                counts[library.Emotions.ToList().IndexOf(selected)]++;
            }
            Assert.That(counts, Is.EqualTo(new[] { 10, 60, 10, 5, 15 }));
            Assert.That(library.TrySelect(-1, out _), Is.False);
            Assert.That(library.TrySelect(100, out _), Is.False);
        }

        [Test]
        public void MissingClipsAndZeroWeightsDoNotEnterTheDraw()
        {
            RuviEmotionLibrary configured = AssetDatabase.LoadAssetAtPath<RuviEmotionLibrary>(LibraryPath);
            RuviEmotionLibrary library = ScriptableObject.CreateInstance<RuviEmotionLibrary>();
            try
            {
                SerializedObject serialized = new SerializedObject(library);
                SerializedProperty emotions = serialized.FindProperty("_emotions");
                emotions.arraySize = 3;
                emotions.GetArrayElementAtIndex(0).FindPropertyRelative("Weight").intValue = 999;
                emotions.GetArrayElementAtIndex(1).FindPropertyRelative("Clip").objectReferenceValue = configured.Emotions[0].Clip;
                emotions.GetArrayElementAtIndex(1).FindPropertyRelative("Weight").intValue = 0;
                emotions.GetArrayElementAtIndex(2).FindPropertyRelative("Clip").objectReferenceValue = configured.Emotions[1].Clip;
                emotions.GetArrayElementAtIndex(2).FindPropertyRelative("Weight").intValue = 10;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(library.TotalWeight, Is.EqualTo(10));
                for (int roll = 0; roll < 10; roll++)
                {
                    Assert.That(library.TrySelect(roll, out RuviEmotionLibrary.Emotion selected), Is.True);
                    Assert.That(selected.Clip, Is.SameAs(configured.Emotions[1].Clip));
                }
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void ClosingInvalidatesThePendingVideoBeforeItsPreparationCallback()
        {
            GameObject root = new GameObject("Ruvi pending video test");
            try
            {
                RuviEmotionLibrary library = AssetDatabase.LoadAssetAtPath<RuviEmotionLibrary>(LibraryPath);
                RuviFaceController controller = root.AddComponent<RuviFaceController>();
                VideoPlayer player = root.AddComponent<VideoPlayer>();
                player.playOnAwake = false;
                player.clip = library.Emotions[0].Clip;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(RuviFaceController).GetField("_player", flags).SetValue(controller, player);
                typeof(RuviFaceController).GetField("_requestedClip", flags).SetValue(controller, player.clip);

                // Edit Mode does not run gameplay lifecycle/video callbacks; exercise their boundary explicitly.
                typeof(RuviFaceController).GetMethod("HandleStateChanged", flags)
                    .Invoke(controller, new object[] { InventoryRobotPresentationState.Closing });
                typeof(RuviFaceController).GetMethod("HandlePrepared", flags)
                    .Invoke(controller, new object[] { player });

                Assert.That(typeof(RuviFaceController).GetField("_requestedClip", flags).GetValue(controller), Is.Null);
                Assert.That(player.isPlaying, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
