namespace Autobot.Common
{
    //The commands for interaction between the server and the client
    public enum MessageType
    {
        Null = 0,
        Left = 1,
        Right = 2,     
        Forward = 4,     
        Back = 5,       
        Map = 6,
        Info = 7,
        Auto = 8,
        Off = 9,
		Hello = 10,
        Ack = 11,
        Sense = 12,
        RemoteControl = 13,
        Speed = 14,
        Turn = 15,
        CorrectPosition = 16,
    }
}