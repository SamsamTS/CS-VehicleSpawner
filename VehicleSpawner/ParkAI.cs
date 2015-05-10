using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

using baseParkAI = ParkAI; // To inherit with same name

namespace VehicleSpawner
{
    /// <summary>
    /// Holds data for each spawner
    /// </summary>
    public class VehicleSpawnerData
    {
        public uint m_vehicleCount = 0;
        public bool m_checkDirection = false;
        public bool m_reverseDirection = false;
        public uint m_reverseCount = 0;
    }

    /// <summary>
    /// ParkAI is extended to add vehicle spawning ability
    /// </summary>
    public class ParkAI : baseParkAI
    {

        [CustomizableProperty("MaximumVehicles", "Spawner")]
        public int m_maxVehicles = 10;
        [CustomizableProperty("OffsetX", "Spawner")]
        public float m_offsetX = 0;
        [CustomizableProperty("OffsetY", "Spawner")]
        public float m_offsetY = 0;
        [CustomizableProperty("VehiclesTypeMin", "Spawner")]
        public int m_vehiclesTypesMin = 0;
        [CustomizableProperty("VehiclesTypeMax", "Spawner")]
        public int m_vehiclesTypesMax = 0;
        [CustomizableProperty("VehiclesType0", "Spawner")]
        public int m_vehiclesTypes0 = 0;
        [CustomizableProperty("VehiclesType1", "Spawner")]
        public int m_vehiclesTypes1 = 0;
        [CustomizableProperty("VehiclesType2", "Spawner")]
        public int m_vehiclesTypes2 = 0;
        [CustomizableProperty("VehiclesType3", "Spawner")]
        public int m_vehiclesTypes3 = 0;
        [CustomizableProperty("VehiclesType4", "Spawner")]
        public int m_vehiclesTypes4 = 0;
        [CustomizableProperty("SpawnerType", "Spawner")]
        public int m_spawnerType = 0;

        private enum SpawningType
        {
            Continuous = 0,
            CitizenVisit = 1,
            CitizenVisitOwnedVehicle = 2
        }

        private Dictionary<ushort, VehicleSpawnerData> m_datas = new Dictionary<ushort, VehicleSpawnerData>();
        public uint[] m_vehiclesTypes;

        //private static int m_prefabOffset = 0;
        //private static int m_prefabCount = 0;

        public override void CreateBuilding(ushort buildingID, ref Building buildingData)
        {
            base.CreateBuilding(buildingID, ref buildingData);

            if (m_spawnerType > (int)SpawningType.CitizenVisitOwnedVehicle) // Sanity check
                m_spawnerType = 0;

            // Storing data for that building
            VehicleSpawnerData data = new VehicleSpawnerData();
            data.m_reverseDirection = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;

            m_datas.Add(buildingID, data);

        }

        public override void BuildingLoaded(ushort buildingID, ref Building buildingData, uint version)
        {
            base.BuildingLoaded(buildingID, ref buildingData, version);

            if (m_spawnerType > (int)SpawningType.CitizenVisitOwnedVehicle) // Sanity check
                m_spawnerType = 0;

            if (m_datas.ContainsKey(buildingID)) m_datas.Clear();

            // Storing data for that building
            VehicleSpawnerData data = new VehicleSpawnerData();
            data.m_reverseDirection = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;

            m_datas.Add(buildingID, data);
        }
        
        public override void ReleaseBuilding(ushort buildingID, ref Building data)
        {
            base.ReleaseBuilding(buildingID, ref data);

            // Releasing data from that building
            m_datas.Remove(buildingID);
        }

        protected override void ManualDeactivation(ushort buildingID, ref Building buildingData)
        {
            base.ManualDeactivation(buildingID, ref buildingData);

            m_datas[buildingID].m_checkDirection = false;

            // Direction reversed if game paused
            if (Singleton<SimulationManager>.instance.SimulationPaused)
                m_datas[buildingID].m_reverseDirection = !m_datas[buildingID].m_reverseDirection;
        }

        public override void BeginRelocating(ushort buildingID, ref Building data)
        {
            base.BeginRelocating(buildingID, ref data);

            // Destroying spawned vehicles to prevent them from flying to the new location
            VehicleManager instance = Singleton<VehicleManager>.instance;
            ushort num = data.m_ownVehicles;

            while (num != 0)
            {
                if (instance.m_vehicles.m_buffer[(int)num].Info.m_vehicleAI is DummyVehicleAI)
                    instance.m_vehicles.m_buffer[(int)num].Unspawn(num);

                num = instance.m_vehicles.m_buffer[(int)num].m_nextOwnVehicle;
            }

            // Reseting some data
            m_datas[buildingID].m_vehicleCount = 0;
            m_datas[buildingID].m_checkDirection = false;
            m_datas[buildingID].m_reverseCount = 0;
        }

        public override string GetLocalizedStats(ushort buildingID, ref Building buildingData)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder(base.GetLocalizedStats(buildingID, ref buildingData) + Environment.NewLine);

            // Displaying the number of visitors
            text.Append("Visitors: ").AppendLine(GetVisitorCount(ref buildingData).ToString());
            
            // Displaying the number of vehicles
            int count = 0;
            int cargo = 0;
            int capacity = 0;
            int outside = 0;
            base.CalculateOwnVehicles(buildingID, ref buildingData, TransferManager.TransferReason.DummyCar, ref count, ref cargo, ref capacity, ref outside);

            string s = count.ToString();
            if (m_maxVehicles != 0)
                s += "/" + m_maxVehicles;

            return text.Append(LocaleFormatter.FormatGeneric("TRANSPORT_LINE_VEHICLECOUNT", new object[] { s })).ToString();

        }

        protected override void ProduceGoods(ushort buildingID, ref Building buildingData, ref Building.Frame frameData, int productionRate, ref Citizen.BehaviourData behaviour, int aliveWorkerCount, int totalWorkerCount, int workPlaceCount, int aliveVisitorCount, int totalVisitorCount, int visitPlaceCount)
        {
            base.ProduceGoods(buildingID, ref buildingData, ref frameData, productionRate, ref behaviour, aliveWorkerCount, totalWorkerCount, workPlaceCount, aliveVisitorCount, totalVisitorCount, visitPlaceCount);
            
            VehicleSpawnerData data = m_datas[buildingID];

            // Only spawn vehicle if no problem
            if(buildingData.m_problems != Notification.Problem.None)
            {
                data.m_checkDirection = false;
                return;
            }

            // Getting the number of vehicles
            int count = 0;
            int cargo = 0;
            int capacity = 0;
            int outside = 0;
            CalculateOwnVehicles(buildingID, ref buildingData, TransferManager.TransferReason.DummyCar, ref count, ref cargo, ref capacity, ref outside);


            // Getting first visiting citizen if necessary
            uint citizenID = 0;
            if (m_spawnerType != (int)SpawningType.Continuous)
            {
                if ((uint)count >= GetVisitorCount(ref buildingData) ||
                    (citizenID = GetFirstCitizen(ref buildingData)) == 0)
                {
                    data.m_checkDirection = false;
                    return;
                }
            }

            // If there are less vehicles than spawned (up to 3) then the direction is probably wrong
            if (data.m_checkDirection && count < Math.Min(3, data.m_vehicleCount) && data.m_reverseCount < 10)
            {
                data.m_reverseDirection = !m_datas[buildingID].m_reverseDirection;
                data.m_checkDirection = false;
                data.m_reverseCount++;
                count = 0;
            }

            if (m_maxVehicles == 0 || count < m_maxVehicles)
            {
                
                // Getting owned vehicle or random vehicle from the given list
                VehicleInfo randomVehicleInfo = null;
                ushort vehicleID = 0;
                Vector3 position;
                Vector3 target;
                

                if (m_spawnerType == (int)SpawningType.CitizenVisitOwnedVehicle)
                {
                    Citizen citizen = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenID];
                    vehicleID = (citizen.m_vehicle == 0) ? citizen.m_vehicle : citizen.m_parkedVehicle;
                    randomVehicleInfo = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID].Info;
                    CalculateSpawnPosition(buildingID, ref buildingData, ref Singleton<SimulationManager>.instance.m_randomizer, randomVehicleInfo, out position, out target);

                }
                else
                {
                    ParseVehicleTypes();

                    int r = Singleton<SimulationManager>.instance.m_randomizer.Int32(0, m_vehiclesTypes.Length - 1);
                    //randomVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(m_vehiclesTypes[r] + (uint)m_prefabOffset);
                    randomVehicleInfo = VehiclePrefabs.GetVehiclePrefab(m_vehiclesTypes[r]);
                    CalculateSpawnPosition(buildingID, ref buildingData, ref Singleton<SimulationManager>.instance.m_randomizer, randomVehicleInfo, out position, out target);
                    
                    if (!Singleton<VehicleManager>.instance.CreateVehicle(out vehicleID, ref Singleton<SimulationManager>.instance.m_randomizer, randomVehicleInfo, position, TransferManager.TransferReason.DummyCar, true, false))
                        return;
                }
                
                // Starting transfer
                TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
                offer.Building = buildingID;
                offer.Position = position;
                offer.Amount = 1;
                offer.Active = true;

                /*if (m_spawnerType != (int)SpawningType.Continuous)
                {
                    // Embarking citizen
                    Citizen citizen = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenID];

                    CitizenInstance citizenData = Singleton<CitizenManager>.instance.m_instances.m_buffer[citizen.m_instance];

                    citizenData.m_flags |= CitizenInstance.Flags.WaitingTransport;
                    citizen.GetCitizenInfo(citizenID).m_citizenAI.SetCurrentVehicle((ushort)citizenID, ref citizenData, vehicleID, 0u, position);

                    //citizen.CurrentLocation = Citizen.Location.Moving;
                    //DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "" + (citizen.CurrentLocation == Citizen.Location.Visit));
                }*/

                Array16<Vehicle> vehicles = Singleton<VehicleManager>.instance.m_vehicles;

                randomVehicleInfo.m_vehicleAI.SetSource(vehicleID, ref vehicles.m_buffer[vehicleID], buildingID);
                randomVehicleInfo.m_vehicleAI.SetTarget(vehicleID, ref vehicles.m_buffer[vehicleID], buildingID);
                randomVehicleInfo.m_vehicleAI.StartTransfer(vehicleID, ref vehicles.m_buffer[vehicleID], TransferManager.TransferReason.DummyCar, offer);

                data.m_checkDirection = true;
                data.m_vehicleCount = (uint)count+1;
            }
        }

        public override void CalculateSpawnPosition(ushort buildingID, ref Building buildingData, ref Randomizer randomizer, VehicleInfo info, out Vector3 position, out Vector3 target)
        {
            if (info != null && info.m_vehicleAI is DummyVehicleAI)
            {
                // Calculate where vehicles spawn and which direction they will go
                uint extra = m_datas[buildingID].m_reverseCount / 2; // Extra offset if both directions are wrong (the vehicle probably doesn't go far enough on the road)
                float offset = m_datas[buildingID].m_reverseDirection ? -5 - extra : 5 + extra;

                float x = -8f * (m_offsetX - (float)buildingData.Width / 2f);
                float y = -8f * m_offsetY;

                position = buildingData.CalculateSidewalkPosition(x, y);
                position.y = Singleton<TerrainManager>.instance.SampleDetailHeight(position);

                target = buildingData.CalculateSidewalkPosition(x + offset, 5 + extra);
                target.y += 0.1f;
            }
            else
            {
                base.CalculateSpawnPosition(buildingID, ref buildingData, ref randomizer, info, out position, out target);
            }
        }

        public override void CalculateUnspawnPosition(ushort buildingID, ref Building buildingData, ref Randomizer randomizer, VehicleInfo info, out Vector3 position, out Vector3 target)
        {
            if (info != null && info.m_vehicleAI is DummyVehicleAI)
            {
                // Calculate where vehicles unspawn and from which direction they will come
                float offset = m_datas[buildingID].m_reverseDirection ? 5 : -5;

                float x = -8f * (m_offsetX - (float)buildingData.Width / 2f);
                float y = -8f * m_offsetY;

                position = buildingData.CalculateSidewalkPosition(x + offset, 5);
                position.y += 0.1f;

                target = buildingData.CalculateSidewalkPosition(x, y);
                target.y = Singleton<TerrainManager>.instance.SampleDetailHeight(target);
            }
            else
            {
                base.CalculateSpawnPosition(buildingID, ref buildingData, ref randomizer, info, out position, out target);
            }
        }


        /// <summary>
        /// Returns the ID of the first citizen visiting the building.
        /// Citizen must be a least young adult.
        /// When CitizenVisitOwnedVehicle is used, the citizen must own a vehicle.
        /// </summary>
        /// <param name="buildingData">Building data</param>
        /// <returns>Citizen ID</returns>
        private uint GetFirstCitizen(ref Building buildingData)
        {
            uint unit = buildingData.m_citizenUnits;
            while (unit != 0)
            {
                CitizenUnit citizenUnit = Singleton<CitizenManager>.instance.m_units.m_buffer[unit];

                for (int i = 0; i < 5; i++)
                {
                    uint citizenID = citizenUnit.GetCitizen(i);
                    if (citizenID == 0) continue;

                    Citizen citizen = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenID];

                    if (m_spawnerType == (int)SpawningType.CitizenVisitOwnedVehicle &&
                        citizen.m_vehicle == 0 && citizen.m_parkedVehicle == 0)
                        continue;

                    if (citizen.CurrentLocation == Citizen.Location.Visit &&
                        citizen.Age > Citizen.AGE_LIMIT_TEEN)
                        return citizenID;
                }

                unit = citizenUnit.m_nextUnit;
            }
            return 0;
        }


        private uint GetVisitorCount(ref Building buildingData)
        {
            uint count = 0;
            uint unit = buildingData.m_citizenUnits;
            while (unit != 0)
            {
                CitizenUnit citizenUnit = Singleton<CitizenManager>.instance.m_units.m_buffer[unit];

                for (int i = 0; i < 5; i++)
                {
                    uint citizenID = citizenUnit.GetCitizen(i);
                    if (citizenID == 0) continue;

                    Citizen citizen = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenID];

                    if (citizen.CurrentLocation == Citizen.Location.Visit)
                        count++;
                }

                unit = citizenUnit.m_nextUnit;
            }
            return count;
        }

        /// <summary>
        /// Parse (sanity check) the vehicle types custom properties.
        /// </summary>
        /// <returns>Array of vehicle types</returns>
        private void ParseVehicleTypes()
        {
            if (m_vehiclesTypes != null) return;

            FastList<uint> vehiclesTypes = new FastList<uint>();

            // Parsing range numbers
            int n1 = m_vehiclesTypesMin;
            int n2 = m_vehiclesTypesMax;

            if (n1 != 0 && n2 != 0 && --n1 <= VehiclePrefabs.Count && --n2 <= VehiclePrefabs.Count)
            {
                if (n1 > n2)
                {
                    int tmp = n1;
                    n1 = n2;
                    n2 = tmp;
                }

                for (int i = n1; i < n2; i++)
                    vehiclesTypes.Add((uint)i);
            }

            string str = "";

            // Parsing single numbers
            for (int i = 0; i < 5; i++)
            {
                int n = 0;
                switch (i)
                {
                    case 0:
                        n = m_vehiclesTypes0;
                        break;
                    case 1:
                        n = m_vehiclesTypes1;
                        break;
                    case 2:
                        n = m_vehiclesTypes2;
                        break;
                    case 3:
                        n = m_vehiclesTypes3;
                        break;
                    case 4:
                        n = m_vehiclesTypes4;
                        break;
                }

                str += n + " ";

                if (n == 0 || --n > VehiclePrefabs.Count) continue;

                vehiclesTypes.Add((uint)n);
            }

            // If no vehicle selected, fall-back to default (Citizen type vehicles)
            if (vehiclesTypes.m_size == 0)
                m_vehiclesTypes = new uint[] { 6, 7, 8, 9, 10, 11 };
            else
                m_vehiclesTypes = vehiclesTypes.ToArray();
        }


    }
}
