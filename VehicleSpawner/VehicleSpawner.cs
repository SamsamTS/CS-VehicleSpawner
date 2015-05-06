using ICities;

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
            get { return "Custom Building AI allowing to make buildings that spawn vehicle."; }
        }
    }
}
