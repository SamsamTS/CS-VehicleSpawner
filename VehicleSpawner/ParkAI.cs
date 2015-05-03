using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using UnityEngine;

//Because ParkAI isn't in a namespace we need to rename it in this scope to name our class ParkAI as well
using baseParkAI = ParkAI;

namespace VehicleSpawner
{
    public class VehicleSpawnerData
    {
        public uint[] m_vehiclesTypes;
        public int m_maxVehicles = 10;
        public bool m_checkDirection = false;
        public bool m_reverseDirection = false;
    }

    public class ParkAI : baseParkAI
    {

        [CustomizableProperty("MaximumVehicles", "Spawner")]
        public int m_maxVehicles = 10;
        [CustomizableProperty("VehiclesTypes", "Spawner")]
        public string m_vehiclesTypesList = String.Empty;
        [CustomizableProperty("OffsetX", "Spawner")]
        public float m_offsetX = 0;
        [CustomizableProperty("OffsetY", "Spawner")]
        public float m_offsetY = 0;

        // A dictionary is needed to hold the data for each individual building
        private System.Collections.Generic.Dictionary<ushort, VehicleSpawnerData> m_datas = new Dictionary<ushort,VehicleSpawnerData>();


        public override void CreateBuilding(ushort buildingID, ref Building buildingData)
        {
            base.CreateBuilding(buildingID, ref buildingData);

            VehicleSpawnerData data = new VehicleSpawnerData();
            data.m_vehiclesTypes =  parseInput();
            data.m_maxVehicles = m_maxVehicles;

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

            if (data.m_maxVehicles == 0 || count < data.m_maxVehicles)
            {

                // If the vehicles we spawned are already back (count=0) then the direction is probably wrong
                if (data.m_checkDirection && count == 0)
                {
                    data.m_reverseDirection = !m_datas[buildingID].m_reverseDirection;
                    data.m_checkDirection = false;
                }

                // Getting random vehicle from the given list
                uint r = Singleton<SimulationManager>.instance.m_randomizer.UInt32(0, (uint)data.m_vehiclesTypes.Length - 1);
                VehicleInfo randomVehicleInfo = PrefabCollection<VehicleInfo>.GetPrefab(data.m_vehiclesTypes[r]);

                if (randomVehicleInfo != null)
                {
                    // Setting up a new vehicle AI
                    DummyVehicleAI dummyAI = new DummyVehicleAI();
                    dummyAI.m_info = randomVehicleInfo;
                    dummyAI.m_passengerCapacity = 1;
                    dummyAI.m_transportInfo = new TransportInfo();
                    dummyAI.m_transportInfo.m_transportType = TransportInfo.TransportType.Bus;
                    dummyAI.m_ticketPrice = 0;
                    randomVehicleInfo.m_vehicleAI.ReleaseAI();
                    randomVehicleInfo.m_vehicleAI = dummyAI;
                    randomVehicleInfo.m_vehicleAI.InitializeAI();

                    // Creating and spawning the vehicle
                    Vector3 position;
                    Vector3 vector;
                    this.CalculateSpawnPosition(buildingID, ref buildingData, ref Singleton<SimulationManager>.instance.m_randomizer, randomVehicleInfo, out position, out vector);

                    ushort num;
                    if (Singleton<VehicleManager>.instance.CreateVehicle(out num, ref Singleton<SimulationManager>.instance.m_randomizer, randomVehicleInfo, position, TransferManager.TransferReason.DummyCar, true, false))
                    {
                        TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
                        offer.Priority = 2 - count;
                        offer.Building = buildingID;
                        offer.Position = position;
                        offer.Amount = 1;
                        offer.Active = true;

                        Array16<Vehicle> vehicles = Singleton<VehicleManager>.instance.m_vehicles;
                        randomVehicleInfo.m_vehicleAI.SetSource(num, ref vehicles.m_buffer[(int)num], buildingID);
                        randomVehicleInfo.m_vehicleAI.SetTarget(num, ref vehicles.m_buffer[(int)num], buildingID);
                        randomVehicleInfo.m_vehicleAI.StartTransfer(num, ref vehicles.m_buffer[(int)num], TransferManager.TransferReason.DummyCar, offer);

                        data.m_checkDirection = true;
                    }
                }
            }
        }

        public override void CalculateSpawnPosition(ushort buildingID, ref Building data, ref Randomizer randomizer, VehicleInfo info, out Vector3 position, out Vector3 target)
        {
            if (info.m_vehicleType == VehicleInfo.VehicleType.Car && info.m_vehicleAI is DummyVehicleAI)
            {
                float offset = m_datas[buildingID].m_reverseDirection ? -5 : 5;
                position = data.CalculateSidewalkPosition(m_offsetX, -m_offsetY);
                target = data.CalculateSidewalkPosition(m_offsetX+offset, 5);
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
                float offset = m_datas[buildingID].m_reverseDirection ? -5 : 5;
                position = data.CalculateSidewalkPosition(m_offsetX+offset, 5);
                target = data.CalculateSidewalkPosition(m_offsetX, -m_offsetY);
            }
            else
            {
                base.CalculateSpawnPosition(buildingID, ref data, ref randomizer, info, out position, out target);
            }
        }

        // TODO : no testing done yet !!!
        private uint[] parseInput()
        {
            // There is only 32 types of vehicles
            const uint max = 32;

            if (m_vehiclesTypesList.Trim() == String.Empty)
            {
                // Default list of vehicles types (citizen type cars)
                return new uint[] { 17, 18, 19, 20, 21, 22 };
            }

            uint[] vehiclesTypes = new uint[] { };

            String[] list = m_vehiclesTypesList.Split(',');
            foreach (string s in list)
            {
                if (s.Trim() == String.Empty) continue;

                if (s.Contains("-"))
                {
                    // Parsing range numbers
                    string[] range = s.Split('-');
                    if (range.Length != 2 || range[0].Trim() == String.Empty || range[1].Trim() == String.Empty) continue;

                    uint n1, n2;

                    try
                    {
                        n1 = Convert.ToUInt32(range[0].Trim());
                        n2 = Convert.ToUInt32(range[1].Trim());
                    }
                    catch (OverflowException) { continue; }
                    catch (FormatException) { continue; }

                    if (n1 > max || n2 > max) continue;

                    if (n1 > n2)
                    {
                        uint tmp = n1;
                        n1 = n2;
                        n2 = tmp;
                    }

                    for (uint i = n1; n1 < n2; i++)
                    {
                        // Filtering out trailers
                        switch(i)
                        {
                            case 3: // Ore truck trailer
                            case 5: // Tractor trailer
                            case 10: // Passenger train trailer
                            case 11: // Cargo train trailer
                            case 31: // Forestry truck trailer
                                continue;
                        }

                        vehiclesTypes[vehiclesTypes.Length] = i;
                    }
                }
                else
                {
                    // Parsing single numbers
                    uint n = 0;

                    try
                    {
                        n = Convert.ToUInt32(s.Trim());
                    }
                    catch (OverflowException) { continue; }
                    catch (FormatException) { continue; }

                    if (n > max) continue;

                    // Filtering out trailers
                    switch (n)
                    {
                        case 3: // Ore truck trailer
                        case 5: // Tractor trailer
                        case 10: // Passenger train trailer
                        case 11: // Cargo train trailer
                        case 31: // Forestry truck trailer
                            continue;
                    }

                    vehiclesTypes[vehiclesTypes.Length] = n;
                }
            }

            // No valid number found, fallback to default
            if (vehiclesTypes.Length == 0)
                return new uint[] { 17, 18, 19, 20, 21, 22 };

            /*string s2 = "";
            foreach(uint i in vehiclesTypes)
                s2 += i + ", ";

            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, s2);*/

            return vehiclesTypes;
        }

    }
}
