using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Builds both hero AnimatorControllers + clips from the sliced sprite
    /// sheets. Params match what CharacterAnimOne/Two drive (Speed, Jumping,
    /// InAir, Crouching, Dead + triggers Jump/Crouch/Death) plus the new
    /// Shoot/Transform/Hurt/Win triggers. 12fps to match the world art.
    /// </summary>
    public static class AnimatorBuilder
    {
        const float Fps = 12f;
        const string OutFolder = "Assets/Art/Generated/Animations";

        [MenuItem("Escape/Build Hero Animators")]
        public static void BuildHeroAnimators()
        {
            EnsureFolder(OutFolder);
            Build("hero1_ember", "Hero1", specialTrigger: "Shoot", specialFrames: "shoot");
            Build("hero2_wisp", "Hero2", specialTrigger: "Transform", specialFrames: "transform");
            AssetDatabase.SaveAssets();
        }

        static void Build(string sheetName, string controllerName, string specialTrigger, string specialFrames)
        {
            Dictionary<string, List<Sprite>> groups = LoadFrameGroups(sheetName);
            if (groups.Count == 0)
            {
                Debug.LogError($"[AnimatorBuilder] No sprites for {sheetName} — run SliceGeneratedSprites first");
                return;
            }

            string folder = $"{OutFolder}/{controllerName}";
            EnsureFolder(folder);

            string controllerPath = $"{folder}/{controllerName}.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Jumping", AnimatorControllerParameterType.Bool);
            controller.AddParameter("InAir", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Win", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(specialTrigger, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idle = AddState(sm, folder, "Idle", Frames(groups, "idle"), loop: true);
            AnimatorState walk = AddState(sm, folder, "Walk", Frames(groups, "walk"), loop: true);
            AnimatorState jump = AddState(sm, folder, "Jump", Frames(groups, "jump"), loop: true);
            AnimatorState fall = AddState(sm, folder, "Fall", Frames(groups, "fall"), loop: true);
            AnimatorState crouch = AddState(sm, folder, "Crouch", Frames(groups, "crouch"), loop: true);
            AnimatorState hurt = AddState(sm, folder, "Hurt", Frames(groups, "hurt"), loop: false);
            AnimatorState death = AddState(sm, folder, "Death", Frames(groups, "death"), loop: false);
            AnimatorState win = AddState(sm, folder, "Win", Frames(groups, "win"), loop: true);
            AnimatorState special = AddState(sm, folder, specialTrigger, Frames(groups, specialFrames), loop: false);

            sm.defaultState = idle;

            // Locomotion.
            Cond(idle, walk, ("Speed", AnimatorConditionMode.Greater, 0.1f));
            Cond(walk, idle, ("Speed", AnimatorConditionMode.Less, 0.1f));

            // Airborne (from both ground states).
            foreach (AnimatorState ground in new[] { idle, walk })
            {
                Cond(ground, jump, ("Jumping", AnimatorConditionMode.If, 0f));
                Cond(ground, fall, ("InAir", AnimatorConditionMode.If, 0f),
                                   ("Jumping", AnimatorConditionMode.IfNot, 0f));
            }
            Cond(jump, fall, ("InAir", AnimatorConditionMode.If, 0f),
                             ("Jumping", AnimatorConditionMode.IfNot, 0f));
            Cond(jump, idle, ("InAir", AnimatorConditionMode.IfNot, 0f));
            Cond(fall, idle, ("InAir", AnimatorConditionMode.IfNot, 0f));

            // Crouch.
            Cond(idle, crouch, ("Crouching", AnimatorConditionMode.If, 0f));
            Cond(walk, crouch, ("Crouching", AnimatorConditionMode.If, 0f));
            Cond(crouch, idle, ("Crouching", AnimatorConditionMode.IfNot, 0f));

            // Special (shoot/transform) from any grounded/air state, exits to idle.
            AnimatorStateTransition anySpecial = sm.AddAnyStateTransition(special);
            anySpecial.AddCondition(AnimatorConditionMode.If, 0f, specialTrigger);
            anySpecial.duration = 0f;
            anySpecial.canTransitionToSelf = false;
            Exit(special, idle);

            // Hurt interrupt, exits back to idle.
            AnimatorStateTransition anyHurt = sm.AddAnyStateTransition(hurt);
            anyHurt.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
            anyHurt.duration = 0f;
            anyHurt.canTransitionToSelf = false;
            Exit(hurt, idle);

            // Death dominates; only leaves when Dead resets (respawn).
            AnimatorStateTransition anyDeath = sm.AddAnyStateTransition(death);
            anyDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            anyDeath.duration = 0f;
            anyDeath.canTransitionToSelf = false;
            Cond(death, idle, ("Dead", AnimatorConditionMode.IfNot, 0f));

            // Win via trigger, loops until scene changes.
            AnimatorStateTransition anyWin = sm.AddAnyStateTransition(win);
            anyWin.AddCondition(AnimatorConditionMode.If, 0f, "Win");
            anyWin.duration = 0f;
            anyWin.canTransitionToSelf = false;

            Debug.Log($"[AnimatorBuilder] Built {controllerPath} ({groups.Count} frame groups)");
        }

        // ---------- helpers ----------

        static Dictionary<string, List<Sprite>> LoadFrameGroups(string sheetName)
        {
            var groups = new Dictionary<string, List<Sprite>>();
            string path = $"Assets/Art/Generated/Characters/{sheetName}.png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(s => s.name).ToList();
            foreach (Sprite sprite in sprites)
            {
                // Names look like hero1_ember_walk_2 → group "walk".
                string frame = sprite.name.Substring(sheetName.Length + 1); // e.g. walk_2
                int underscore = frame.LastIndexOf('_');
                string group = underscore > 0 ? frame.Substring(0, underscore) : frame;
                if (!groups.TryGetValue(group, out List<Sprite> list))
                    groups[group] = list = new List<Sprite>();
                list.Add(sprite);
            }
            foreach (List<Sprite> list in groups.Values)
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return groups;
        }

        static Sprite[] Frames(Dictionary<string, List<Sprite>> groups, string key)
        {
            return groups.TryGetValue(key, out List<Sprite> list)
                ? list.ToArray()
                : System.Array.Empty<Sprite>();
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string folder, string name,
                                      Sprite[] frames, bool loop)
        {
            AnimationClip clip = new AnimationClip { frameRate = Fps, name = name };
            if (frames.Length > 0)
            {
                var binding = new EditorCurveBinding
                {
                    type = typeof(SpriteRenderer),
                    path = string.Empty,
                    propertyName = "m_Sprite",
                };
                var keys = new ObjectReferenceKeyframe[frames.Length];
                for (int i = 0; i < frames.Length; i++)
                    keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = frames[i] };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, $"{folder}/{name}.anim");

            AnimatorState state = sm.AddState(name);
            state.motion = clip;
            state.writeDefaultValues = true;
            return state;
        }

        static void Cond(AnimatorState from, AnimatorState to,
                         params (string param, AnimatorConditionMode mode, float value)[] conditions)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            foreach (var c in conditions)
                t.AddCondition(c.mode, c.value, c.param);
        }

        static void Exit(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 1f;
            t.duration = 0f;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
