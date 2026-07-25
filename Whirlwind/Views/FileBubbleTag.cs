using System.Windows.Controls;
namespace Whirlwind.Views
{
    public class FileBubbleTag
    {
        public int MessageId { get; set; }
        public string FilePath { get; set; }
        public ProgressBar ProgressBar { get; set; }
        public string TargetIp { get; set; }
    }
}