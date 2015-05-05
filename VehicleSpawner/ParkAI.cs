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
        public uint[] m_vehiclesTypes;
        public uint m_maxVehicles = 10;
        public uint m_vehicleCount = 0;
        public float m_offsetX = 0;
        public float m_offsetY = 0;
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
        public uint m_maxVehicles = 10;
        [CustomizableProperty("OffsetX", "Spawner")]
        public float m_offsetX = 0;
        [CustomizableProperty("OffsetY", "Spawner")]
        public float m_offsetY = 0;
        [CustomizableProperty("VehiclesTypeMin", "Spawner")]
        public uint m_vehiclesTypesMin = 0;
        [CustomizableProperty("VehiclesTypeMax", "Spawner")]
        public uint m_vehiclesTypesMax = 0;
        [CustomizableProperty("VehiclesType0", "Spawner")]
        public uint m_vehiclesTypes0 = 0;
        [CustomizableProperty("VehiclesType1", "Spawner")]
        public uint m_vehiclesTypes1 = 0;
        [CustomizableProperty("VehiclesType2", "Spawner")]
        public uint m_vehiclesTypes2 = 0;
        [CustomizableProperty("VehiclesType3", "Spawner")]
        public uint m_vehiclesTypes3 = 0;
        [CustomizableProperty("VehiclesType4", "Spawner")]
        public uint m_vehiclesTypes4 = 0;


        private System.Collections.Generic.Dictionary<ushort, VehicleSpawnerData> m_datas = new Dictionary<ushort,VehicleSpawnerData>();
        private static int m_prefabOffset = 0;


        public override void CreateBuilding(ushort buildingID, ref Building buildingData)
        {
            base.CreateBuilding(buildingID, ref buildingData);

            clonePrefabCollection();

            VehicleSpawnerData data = new VehicleSpawnerData();
            data.m_vehiclesTypes =  parseInput();
            data.m_maxVehicles = m_maxVehicles;
            data.m_offsetX = -8f * (m_offsetX - (float)buildingData.Width / 2f);
            data.m_offsetY = -8f * m_offsetY;
            data.m_reverseDirection = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;

            m_datas.Add(buildingID, data);
        }

        public override void ReleaseBuilding(ushort buildingID, ref Building data)
        {
            base.ReleaseBuilding(buildingID, ref data);

            m_datas.Remove(buildingID);
        }

        protected override void ManualDeactivation(ushort buildingID, ref Building buildingData)
        {
            base.ManualDeactivation(buildingID, ref buildingData);

            m_datas[buildingID].m_checkDirection = false;

            // Direction reversed if game paused
            if (Singleton<SimulationManager>.instance.SimulationPaused)
            {
                m_datas[buildingID].m_reverseCount = 0;
                m_datas[buildingID].m_reverseDirection = !m_datas[buildingID].m_reverseDirection;
            }
        }

        public override void BeginRelocating(ushort buildingID, ref Building data)
        {
            base.EndRelocating(buildingID, ref data);

            // Destroying spawned vehicles to prevent them from flying to the new location
            VehicleManager instance = Singleton<VehicleManager>.instance;
            ushort num = data.m_ownVehicles;

            while (num != 0)
            {
                if (instance.m_vehicles.m_buffer[(int)num].Info.m_vehicleAI is DummyVehicleAI)
                    instance.m_vehicles.m_buffer[(int)num].Unspawn(num);

                num = instance.m_vehicles.m_buffer[(int)num].m_nextOwnVehicle;
            }

            m_datas[buildingID].m_vehicleCount = 0;
            m_datas[buildingID].m_checkDirection = false;
            m_datas[buildingID].m_reverseCount = 0;
        }

        public override string GetLocalizedStats(ushort buildingID, ref Building buildingData)
        {
            string text = base.GetLocalizedStats(buildingID, ref buildingData) + Environment.NewLine;
            
            // Displaying the number of vehicles
            int count = 0;
            int cargo = 0;
            int capacity = 0;
            int outside = 0;
            base.CalculateOwnVehicles(buildingID, ref buildingData, TransferManager.TransferReason.DummyCar, ref count, ref cargo, ref capacity, ref outside);

            string s = count.ToString();
            if (m_datas[buildingID].m_maxVehicles != 0)
                s += "/" + m_datas[buildingID].m_maxVehicles;

            return text + LocaleFormatter.FormatGeneric("TRANSPORT_LINE_VEHICLECOUNT", new object[] { s });

        }

        protected override void ProduceGoods(ushort buildingID, ref Building buildingData, ref Building.Frame frameData, int productionRate, ref Citizen.BehaviourData behaviour, int aliveWorkerCount, int totalWorkerCount, int workPlaceCount, int aliveVisitorCount, int totalVisitorCount, int visitPlaceCount)
        {
            base.ProduceGoods(buildingID, ref buildingData, ref frameData, productionRate, ref behaviour, aliveWorkerCount, totalWorkerCount, workPlaceCount, aliveVisitorCount, totalVisitorCount, visitPlaceCount);

            // Getting the number of vehicles
            int count = 0;
            int cargo = 0;
            int capacity = 0;
            int outside = 0;
            base.CalculateOwnVehicles(buildingID, ref buildingData, TransferManager.TransferReason.DummyCar, ref count, ref cargo, ref capacity, ref outside);

            VehicleSpawnerData data = m_datas[buildingID];

            // If there are less vehicles than spawned (up to 3) then the direction is probably wrong
            if (data.m_checkDirection && count < Math.Min(3, data.m_vehicleCount) && data.m_reverseCount < 10)
            {
                data.m_reverseDirection = !m_datas[buildingID].m_reverseDirection;
                data.m_checkDirection = false;
                data.m_reverseCount++;
                count = 0;
            }

            if (data.m_maxVehicles == 0 || count < data.m_maxVehicles)
            {
                // Getting random vehicle from the given list
                int r = Singleton<SimulationManager>.instance.m_randomizer.Int32(0, data.m_vehiclesTypes.Length - 1);
                VehicleInfo randomVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(data.m_vehiclesTypes[r]+(uint)m_prefabOffset);

                // Creating and spawning the vehicle
                Vector3 position;
                Vector3 vector;
                this.CalculateSpawnPosition(buildingID, ref buildingData, ref Singleton<SimulationManager>.instance.m_randomizer, randomVehicleInfo, out position, out vector);

                ushort num;
                if (Singleton<VehicleManager>.instance.CreateVehicle(out num, ref Singleton<SimulationManager>.instance.m_randomizer, randomVehicleInfo, position, TransferManager.TransferReason.DummyCar, true, false))
                {
                    TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
                    offer.Building = buildingID;
                    offer.Position = position;
                    offer.Amount = 1;
                    offer.Active = true;

                    Array16<Vehicle> vehicles = Singleton<VehicleManager>.instance.m_vehicles;

                    randomVehicleInfo.m_vehicleAI.SetSource(num, ref vehicles.m_buffer[num], buildingID);
                    randomVehicleInfo.m_vehicleAI.SetTarget(num, ref vehicles.m_buffer[num], buildingID);
                    randomVehicleInfo.m_vehicleAI.StartTransfer(num, ref vehicles.m_buffer[num], TransferManager.TransferReason.DummyCar, offer);

                    data.m_checkDirection = true;
                    data.m_vehicleCount = (uint)count+1;
                }
            }
        }

        public override void CalculateSpawnPosition(ushort buildingID, ref Building data, ref Randomizer randomizer, VehicleInfo info, out Vector3 position, out Vector3 target)
        {
            if (info.m_vehicleType == VehicleInfo.VehicleType.Car && info.m_vehicleAI is DummyVehicleAI)
            {
                uint extra = m_datas[buildingID].m_reverseCount / 2; // extra offset if both directions are wrong (probably a curved road)
                float offset = m_datas[buildingID].m_reverseDirection ? -5 - extra : 5 + extra;
                position = data.CalculateSidewalkPosition(m_datas[buildingID].m_offsetX, m_datas[buildingID].m_offsetY);
                position.y = Singleton<TerrainManager>.instance.SampleDetailHeight(position);
                target = data.CalculateSidewalkPosition(m_datas[buildingID].m_offsetX + offset, 5 + extra);
                target.y += 0.1f;
            }
            else
            {
                base.CalculateSpawnPosition(buildingID, ref data, ref randomizer, info, out position, out target);
            }
        }
        public override void CalculateUnspawnPosition(ushort buildingID, ref Building data, ref Randomizer randomizer, VehicleInfo info, out Vector3 position, out Vector3 target)
        {
            if (info.m_vehicleType == VehicleInfo.VehicleType.Car && info.m_vehicleAI is DummyVehicleAI)
            {
                float offset = m_datas[buildingID].m_reverseDirection ? 5 : -5;
                position = data.CalculateSidewalkPosition(m_datas[buildingID].m_offsetX + offset, 5);
                position.y += 0.1f;
                target = data.CalculateSidewalkPosition(m_datas[buildingID].m_offsetX, m_datas[buildingID].m_offsetY);
                target.y = Singleton<TerrainManager>.instance.SampleDetailHeight(target);
            }
            else
            {
                base.CalculateSpawnPosition(buildingID, ref data, ref randomizer, info, out position, out target);
            }
        }

        private static void clonePrefabCollection()
        {
            if (m_prefabOffset != 0) return;
            m_prefabOffset = PrefabCollection<VehicleInfo>.PrefabCount();

            ItemClass itemClass = new ItemClass();
            itemClass.m_service = ItemClass.Service.Beautification;
            itemClass.m_subService = ItemClass.SubService.None;
            itemClass.m_level = ItemClass.Level.None;

            for (int i = 0; i <= m_prefabOffset; i++)
            {
                // Cloning prefab
                VehicleInfo source = PrefabCollection<VehicleInfo>.GetPrefab((uint)i);
                VehicleInfo prefab = new GameObject("Dummy " + source.GetLocalizedTitle()).AddComponent<VehicleInfo>();
                Clone<VehicleInfo>(source, ref prefab);
                prefab.m_generatedInfo = ScriptableObject.CreateInstance<VehicleInfoGen>();
                prefab.m_generatedInfo.name = prefab.name + " (GeneratedInfo)";
                prefab.m_Atlas = null;
                prefab.m_Thumbnail = string.Empty;
                prefab.m_lodObject = null;
                prefab.CalculateGeneratedInfo();

                if (prefab.m_vehicleType == VehicleInfo.VehicleType.Car)
                {
                    // Setting up a new vehicle AI
                    DummyVehicleAI dummyAI = new GameObject("DummyVehicleAI").AddComponent<DummyVehicleAI>();
                    dummyAI.m_info = prefab;
                    dummyAI.m_passengerCapacity = 1;
                    dummyAI.m_transportInfo = new TransportInfo();
                    dummyAI.m_transportInfo.m_transportType = TransportInfo.TransportType.Bus;
                    dummyAI.m_ticketPrice = 0;

                    prefab.m_vehicleAI = dummyAI;
                    prefab.m_vehicleAI.InitializeAI();

                }

                // Adding prefab
                prefab.m_class = itemClass;
                PrefabCollection<VehicleInfo>.InitializePrefabs("Dummy Vehicle", prefab, "");
                PrefabCollection<VehicleInfo>.BindPrefabs();
            }
        }

        public static void Clone<T>(T src, ref T dst)
        {
            FieldInfo[] fields = src.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach(FieldInfo f in fields)
                f.SetValue(dst, f.GetValue(src));
        }

        private uint[] parseInput()
        {
            List<uint> vehiclesTypes = new List<uint>();

            // Parsing range numbers
            uint n1 = 1;//m_vehiclesTypesMin;
            uint n2 = 33;//m_vehiclesTypesMax;

            if (n1 != 0 && n2 != 0 && --n1 <= m_prefabOffset && --n2 <= m_prefabOffset)
            {
                if (n1 > n2)
                {
                    uint tmp = n1;
                    n1 = n2;
                    n2 = tmp;
                }

                for (uint i = n1; i < n2; i++)
                {
                    // Filtering out trailers
                    switch (i)
                    {
                        case 3: // Ore truck trailer
                        case 5: // Tractor trailer
                        case 10: // Passenger train trailer
                        case 11: // Cargo train trailer
                        case 31: // Forestry truck trailer
                            break;
                        default:
                            vehiclesTypes.Add(i);
                            break;
                    }

                }
            }

            // Parsing single numbers
            for (int i = 0; i < 5; i++)
            {
                uint n = 0;
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

                if (n == 0 || --n > m_prefabOffset) continue;

                // Filtering out trailers
                switch (n)
                {
                    case 3: // Ore truck trailer
                    case 5: // Tractor trailer
                    case 10: // Passenger train trailer
                    case 11: // Cargo train trailer
                    case 31: // Forestry truck trailer
                        break;
                    default:
                        vehiclesTypes.Add(n);
                        break;
                }
            }

            // If no vehicle selected, fall-back to default
            if (vehiclesTypes.Count == 0)
                return new uint[] { 17, 18, 19, 20, 21, 22 };

            return vehiclesTypes.ToArray();
        }

    }
}
