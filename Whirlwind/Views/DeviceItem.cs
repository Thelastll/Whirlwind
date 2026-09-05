using System.ComponentModel;

namespace Whirlwind.Views
{
    internal class DeviceItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Ip { get; set; }
        public sbyte Muted { get; set; }
        public sbyte Blocked { get; set; }
    }
}
