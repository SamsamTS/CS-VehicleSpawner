using ICities;
using ColossalFramework;
using ColossalFramework.Math;
using System;
using UnityEngine;

namespace VehicleSpawner
{
    public class VehicleSpawner : IUserMod
    {

        public string Name
        {
            get { return "CarSpawnerAI"; }
        }

        public string Description
        {
            get { return "Custom AI that spawn cars."; }
        }
    }


}
