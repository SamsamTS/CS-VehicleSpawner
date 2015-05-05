using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace VehicleSpawner
{
    // BusAI for right now (or forever... it works)

    /// <summary>
    /// Custom AI for spawned vehicle
    /// </summary>
    public class DummyVehicleAI : BusAI
    {
        public override void CreateVehicle(ushort vehicleID, ref Vehicle data)
        {
            base.CreateVehicle(vehicleID, ref data);
        }

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

        // Better calculation of the end position
        protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData)
        {
            Building building;
            if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) != Vehicle.Flags.None && vehicleData.m_sourceBuilding != 0)
                building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)vehicleData.m_sourceBuilding];
            else if (vehicleData.m_targetBuilding != 0)
                building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)vehicleData.m_targetBuilding];
            else
                return base.StartPathFind(vehicleID, ref vehicleData);

            if (building.Info.m_buildingAI is ParkAI)
            {
                Vector3 position;
                Vector3 target;
                building.Info.m_buildingAI.CalculateUnspawnPosition(vehicleData.m_sourceBuilding, ref building, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleData.Info, out position, out target);

                return this.StartPathFind(vehicleID, ref vehicleData, vehicleData.m_targetPos3, position);
            }

            return base.StartPathFind(vehicleID, ref vehicleData);
        }
    }

}
