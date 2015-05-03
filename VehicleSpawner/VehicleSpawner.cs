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
            get { return "VehicleSpawner"; }
        }

        public string Description
        {
            get { return "Custom Building AI allowing to make parks that spawn vehicle."; }
        }
    }


}
