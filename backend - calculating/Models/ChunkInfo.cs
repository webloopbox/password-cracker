namespace backend___calculating.Models
{
    public class ChunkInfo
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }

        public ChunkInfo() {
            this.StartLine = 0;
            this.EndLine = 0;
        }
    }
}