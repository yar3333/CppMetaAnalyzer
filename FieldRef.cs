namespace CppMetaAnalyzer
{
    class FieldRef
    {
        public string Name { get; set; }
        public string RefFrom { get; set; }

        public FieldRef(string name, string refFrom)
        {
            Name = name;
            RefFrom = refFrom;
        }
    }
}
