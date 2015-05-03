using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using System;
using UnityEngine;

namespace VehicleSpawner
{
    // BusAI for right now (or forever... it works)
    public class DummyVehicleAI : BusAI
    {
        // Restoring GetColor from VehicleAI we don't want all blue vehicles
        public override Color GetColor(ushort vehicleID, ref Vehicle data, InfoManager.InfoMode infoMode)
        {
            if (infoMode != InfoManager.InfoMode.None)
            {
                return Singleton<InfoManager>.instance.m_properties.m_neutralColor;
            }
            if (!this.m_info.m_useColorVariations)
            {
                return this.m_info.m_color0;
            }
            Randomizer randomizer = new Randomizer((int)vehicleID);
            switch (randomizer.Int32(4u))
            {
                case 0:
                    return this.m_info.m_color0;
                case 1:
                    return this.m_info.m_color1;
                case 2:
                    return this.m_info.m_color2;
                case 3:
                    return this.m_info.m_color3;
                default:
                    return this.m_info.m_color0;
            }
        }
    }

}
