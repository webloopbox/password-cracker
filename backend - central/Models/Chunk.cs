using System;

namespace backend___central
{
    public class Chunk
    {
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public DateTime firstDateTime { get; set; }

        public Chunk(int StartLine, int EndLine, DateTime firstDateTime) {
            this.StartLine = StartLine;
            this.EndLine = EndLine;
            this.firstDateTime = firstDateTime;
        }
    }
}