namespace CustomProtocol.Net.Exceptions
{
    public class DamagedMessageException : Exception
    {

        public CustomProtocolMessage Message;
        public DamagedMessageException(CustomProtocolMessage message):base("Message was damaged")
        {
            Message = message;
        }
    }
}