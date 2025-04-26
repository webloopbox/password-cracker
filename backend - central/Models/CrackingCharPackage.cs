using System.Collections.Generic;

namespace backend___central
{
    public class CrackingCharPackage
    {
        public List<string> CharPortions { get; set; }

        public CrackingCharPackage()
        {
            CharPortions = new List<string>();
        }

        public CrackingCharPackage(List<string> charPortions)
        {
            CharPortions = charPortions;
        }
    }
}