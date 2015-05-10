using ICities;
using UnityEngine;

using System;
using System.Reflection;

namespace VehicleSpawner
{
    class VehiclePrefabs : LoadingExtensionBase
    {

        //private static int m_prefabOffset = 0;
        private static FastList<VehicleInfo> m_prefabs = new FastList<VehicleInfo>();

        public static int Count
        {
            get
            {
                if (m_prefabs.m_size == 0) ClonePrefabCollection();
                return m_prefabs.m_size;
            }
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            m_prefabs.SetCapacity(0);
        }


        public static VehicleInfo GetVehiclePrefab(uint id)
        {
            if (m_prefabs.m_size == 0) ClonePrefabCollection();
            if (id >= m_prefabs.m_size) return null;

            return m_prefabs.m_buffer[id];
            // PrefabCollection<VehicleInfo>.GetPrefab(id);
        }

        private static void ClonePrefabCollection()
        {
            if (PrefabCollection<VehicleInfo>.LoadedCount() == 0 ||
                PrefabCollection<VehicleInfo>.LoadedCount() != PrefabCollection<VehicleInfo>.PrefabCount())
                return;

            int count = PrefabCollection<VehicleInfo>.PrefabCount();

            ItemClass itemClass = new ItemClass();
            itemClass.m_service = ItemClass.Service.Beautification;
            itemClass.m_subService = ItemClass.SubService.None;
            itemClass.m_level = ItemClass.Level.None;

            for (int i = 0; i < count; i++)
            {
                // Cloning prefab
                VehicleInfo source = PrefabCollection<VehicleInfo>.GetPrefab((uint)i);

                if (source.m_vehicleType != VehicleInfo.VehicleType.Car ||
                    source.name.ToLower().Contains("trailer")) // Safe?
                    continue;

                VehicleInfo prefab = new GameObject("Dummy " + source.name).AddComponent<VehicleInfo>();
                Clone<VehicleInfo>(source, ref prefab);

                // Setting up a new vehicle AI
                DummyVehicleAI dummyAI = new GameObject("DummyVehicleAI").AddComponent<DummyVehicleAI>();
                dummyAI.m_info = prefab;
                dummyAI.m_passengerCapacity = 1;
                dummyAI.m_transportInfo = new TransportInfo();
                dummyAI.m_transportInfo.m_transportType = TransportInfo.TransportType.Bus;
                dummyAI.m_ticketPrice = 0;

                prefab.m_vehicleAI = dummyAI;
                prefab.m_vehicleAI.InitializeAI();

                // Adding prefab
                prefab.m_class = itemClass;
                PrefabCollection<VehicleInfo>.InitializePrefabs("Dummy Vehicle", prefab, null);
                m_prefabs.Add(prefab);
            }

            PrefabCollection<VehicleInfo>.BindPrefabs();

            //m_prefabOffset = PrefabCollection<VehicleInfo>.PrefabCount() - count;
            TrimPrefabCollection(count);

            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "Prefabs: " + m_prefabs.m_size + "/" + count);
        }


        /// <summary>
        /// Makes a copy of existing vehicle prefabs and add custom AI to them
        /// </summary>
        /*private static void ClonePrefabCollection()
        {
            if (m_prefabOffset != 0) return;
            //TrimPrefabCollection(33);
            m_prefabOffset = PrefabCollection<VehicleInfo>.PrefabCount();

            ItemClass itemClass = new ItemClass();
            itemClass.m_service = ItemClass.Service.Beautification;
            itemClass.m_subService = ItemClass.SubService.None;
            itemClass.m_level = ItemClass.Level.None;

            for (int i = 0; i < m_prefabOffset; i++)
            {
                // Cloning prefab
                VehicleInfo source = PrefabCollection<VehicleInfo>.GetPrefab((uint)i);

                if (source.m_vehicleType != VehicleInfo.VehicleType.Car ||
                    source.name.ToLower().Contains("trailer")) // Safe?
                    continue;

                VehicleInfo prefab = new GameObject("Dummy " + source.name).AddComponent<VehicleInfo>();
                Clone<VehicleInfo>(source, ref prefab);

                // Setting up a new vehicle AI
                DummyVehicleAI dummyAI = new GameObject("DummyVehicleAI").AddComponent<DummyVehicleAI>();
                dummyAI.m_info = prefab;
                dummyAI.m_passengerCapacity = 1;
                dummyAI.m_transportInfo = new TransportInfo();
                dummyAI.m_transportInfo.m_transportType = TransportInfo.TransportType.Bus;
                dummyAI.m_ticketPrice = 0;

                prefab.m_vehicleAI = dummyAI;
                prefab.m_vehicleAI.InitializeAI();

                // Adding prefab
                //TODO :prefab.m_class = itemClass;
                PrefabCollection<VehicleInfo>.InitializePrefabs("Dummy Vehicle", prefab, null);
            }

            PrefabCollection<VehicleInfo>.BindPrefabs();

            m_prefabOffset = PrefabCollection<VehicleInfo>.PrefabCount() - m_prefabOffset;
        }*/

        private static void Clone<T>(T src, ref T dst)
        {
            FieldInfo[] fields = src.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (FieldInfo f in fields)
                f.SetValue(dst, f.GetValue(src));
        }

        private static void TrimPrefabCollection(int i)
        {
            FieldInfo f = (typeof(PrefabCollection<VehicleInfo>)).GetField("m_simulationPrefabs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var v = f.GetValue(null);

            FieldInfo f2 = v.GetType().GetField("m_size", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            f2.SetValue(v, i);
        }
    }
}
