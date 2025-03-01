using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using Classic;
using Overload;
using Debug = Classic.Debug;

namespace Mod_CM_D1_Models
{
    [HarmonyPatch(typeof(RobotManager), "SpawnRobotMinimal")]
    class PatchSpawnRobot
    {
        static int CueOffset;
        static bool InitDone = false;

        private static FieldInfo _UnityAudio_m_m_sound_effects_Field = typeof(UnityAudio).GetField("m_sound_effects", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo _UnityAudio_m_m_sound_effects_volume_Field = typeof(UnityAudio).GetField("m_sound_effects_volume", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo _UnityAudio_m_m_sound_effects_pitch_amt_Field = typeof(UnityAudio).GetField("m_sound_effects_pitch_amt", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo _UnityAudio_m_m_sound_effects_cooldown_Field = typeof(UnityAudio).GetField("m_sound_effects_cooldown", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Init()
        {
            if (InitDone)
                return;
            InitDone = true;

            UnityAudio audio = GameManager.m_audio;

            var sound_effects = (AudioClip[])_UnityAudio_m_m_sound_effects_Field.GetValue(audio);
            var sound_effects_volume = (float[])_UnityAudio_m_m_sound_effects_volume_Field.GetValue(audio);
            var sound_effects_pitch_amt = (float[])_UnityAudio_m_m_sound_effects_pitch_amt_Field.GetValue(audio);
            var sound_effects_cooldown = (float[])_UnityAudio_m_m_sound_effects_cooldown_Field.GetValue(audio);

            int numSounds = GetClassic.pig.soundCount;
            int effectsOffset = sound_effects.Length;
            int effectsSize = effectsOffset + numSounds;

            Array.Resize(ref sound_effects, effectsSize);
            Array.Resize(ref sound_effects_volume, effectsSize);
            Array.Resize(ref sound_effects_pitch_amt, effectsSize);
            Array.Resize(ref sound_effects_cooldown, effectsSize);

            _UnityAudio_m_m_sound_effects_Field.SetValue(audio, sound_effects);
            _UnityAudio_m_m_sound_effects_volume_Field.SetValue(audio, sound_effects_volume);
            _UnityAudio_m_m_sound_effects_pitch_amt_Field.SetValue(audio, sound_effects_pitch_amt);
            _UnityAudio_m_m_sound_effects_cooldown_Field.SetValue(audio, sound_effects_cooldown);

            CueOffset = SFXCueManager.cue_data.Count;

            for (int soundIdx = 0; soundIdx < numSounds; soundIdx++)
            {
                var classicSound = GetClassic.pig.sounds[soundIdx];
                int numSamples = classicSound.length;
                var soundData = new byte[numSamples];
                GetClassic.pig.CopySound(classicSound, soundData, 0);

                var clipData = new float[numSamples];
                for (int i = 0; i < numSamples; i++)
                    clipData[i] = (soundData[i] - 128) / 128.0f;

                var clip = AudioClip.Create("classic" + soundIdx, numSamples, 1, 11025, false);
                clip.SetData(clipData, 0);

                int effectsIdx = effectsOffset + soundIdx;
                sound_effects[effectsIdx] = clip;
                sound_effects_volume[effectsIdx] = 1.0f;
                sound_effects_pitch_amt[effectsIdx] = 0f;
                sound_effects_cooldown[effectsIdx] = 0f;

                var cueData = new SFXCueData();
                cueData.layers[0].elements[0].effect = (SoundEffect)effectsIdx;
                SFXCueManager.cue_data.Add(cueData);
            }

            Debug.Log("Added " + numSounds + " classic sounds CueOffset=" + CueOffset + " CueData.Count=" + SFXCueManager.cue_data.Count);
        }

        static void Postfix(GameObject __result)
        {
            if (Overload.GameplayManager.m_game_type != Overload.GameType.CHALLENGE)
                return;
            if (!GetClassic.ClassicInit())
                return;
			if (!InitDone)
				Init();
            var gameObject = __result;
            foreach (var ren in gameObject.GetComponentsInChildren<MeshRenderer>())
                ren.enabled = false;
            GameObject classicModel = GetClassic.InstantiateRobot(UnityEngine.Random.Range(0, GetClassic.NumRobots));
            classicModel.layer = 11;
            classicModel.transform.parent = gameObject.transform;
            classicModel.transform.localPosition = new Vector3();
            classicModel.transform.localScale = new Vector3(.3f, .3f, .3f);

            var robotType = classicModel.GetComponent<ClassicRobotInfo>().m_robot_type;
            var robotInfo = GetClassic.ClassicData.RobotInfo[robotType];

            var robot = gameObject.GetComponent<Robot>();

            var seeSound = GetClassic.ClassicData.Sounds[robotInfo.see_sound];
            var attackSound = GetClassic.ClassicData.Sounds[robotInfo.attack_sound];

            robot.m_sound_alert = seeSound == 255 ? SFXCue.none : (SFXCue)(CueOffset + seeSound);
            robot.m_sound_angry = attackSound == 255 ? SFXCue.none : (SFXCue)(CueOffset + attackSound);

            Debug.Log("createmodels robot0 component see=" + GetClassic.ClassicRobots[0].GetComponent<ClassicRobotInfo>().m_robot_info.see_sound);
            Debug.Log("createmodels robot-type component see=" + GetClassic.ClassicRobots[robotType].GetComponent<ClassicRobotInfo>().m_robot_info.see_sound);
            Debug.Log("SpawnRobot type=" + robotType + " see=" + robotInfo.see_sound + "=" + seeSound + " attack=" + robotInfo.attack_sound + "=" + attackSound + " alert=" + robot.m_sound_alert + " angry=" + robot.m_sound_angry);
        }
    }
}
