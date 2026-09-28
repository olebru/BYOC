namespace BYOCCore
{
    // The register that points at the next program cell. It has the standard register lines; inc advances it.
    // It is its own type so tools can tell which register is the program counter.
    public class ProgramCounter : Register
    {
        public ProgramCounter(string DeviceName, string DeviceID, Bus bus) : base(DeviceName, DeviceID, bus)
        {
        }
    }
}
