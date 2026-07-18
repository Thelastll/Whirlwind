namespace Whirlwind.Views
{
    internal class ChatMessage
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string Date { get; set; }
        public int MessageType { get; set; }
        public bool IsMyMessage { get; set; }

        public string DisplayText
        {
            get
            {
                if (MessageType == 2 && Text != null)
                {
                    int slashIndex = Text.LastIndexOf('/');
                    if (slashIndex >= 0 && slashIndex < Text.Length - 1)
                        return Text.Substring(slashIndex + 1);
                }

                return Text;
            }
        }
    }

}
