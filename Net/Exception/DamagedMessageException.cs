namespace CustomProtocol.Net.Exceptions
{
    public class DamagedMessageException : Exception
    {
        public DamagedMessageException():base("Message was damaged"){}
    }
}