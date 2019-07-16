using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace mod_cm_d1_models
{
    [HarmonyPatch(typeof(RobotManager), "SpawnRobotMinimal")]
    class mod_cm_d1_models
    {
        static void Postfix(GameObject __result)
        {
            if (Overload.GameplayManager.m_game_type != Overload.GameType.CHALLENGE)
                return;
            if (!Classic.GetClassic.ClassicInit())
                return;
            var gameObject = __result;
            foreach (var ren in gameObject.GetComponentsInChildren<MeshRenderer>())
                ren.enabled = false;
            GameObject classicModel = Classic.GetClassic.InstantiateRobot(UnityEngine.Random.Range(0, Classic.GetClassic.NumRobots));
            classicModel.layer = 11;
            classicModel.transform.parent = gameObject.transform;
            classicModel.transform.localPosition = new Vector3();
            classicModel.transform.localScale = new Vector3(.3f, .3f, .3f);
        }
    }
}
