using System.Collections.Generic;

namespace WordWorld.Detection
{
    /// <summary>COCO-80 class names in model output order, plus Word World's household allowlist.</summary>
    public static class CocoClasses
    {
        public static readonly string[] Names =
        {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
            "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog",
            "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella",
            "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball", "kite",
            "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket", "bottle",
            "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple", "sandwich", "orange",
            "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch", "potted plant",
            "bed", "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard", "cell phone",
            "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase", "scissors",
            "teddy bear", "hair drier", "toothbrush",
        };

        // CLAUDE.md section 3: only household/office classes are ever labelled.
        private static readonly HashSet<string> s_household = new HashSet<string>
        {
            "person", "chair", "couch", "bed", "dining table", "potted plant", "tv", "laptop", "mouse",
            "keyboard", "remote", "cell phone", "book", "clock", "vase", "cup", "bottle", "wine glass",
            "bowl", "fork", "knife", "spoon", "banana", "apple", "orange", "scissors", "backpack",
            "handbag", "umbrella", "teddy bear", "toothbrush", "sink", "refrigerator", "microwave",
            "oven", "toaster", "suitcase",
        };

        private static readonly bool[] s_isHousehold = BuildHouseholdTable();

        public static int Count => Names.Length;

        public static string NameOf(int classId) =>
            classId >= 0 && classId < Names.Length ? Names[classId] : "unknown";

        public static bool IsHousehold(int classId) =>
            classId >= 0 && classId < s_isHousehold.Length && s_isHousehold[classId];

        private static bool[] BuildHouseholdTable()
        {
            var table = new bool[Names.Length];
            for (int i = 0; i < Names.Length; i++) table[i] = s_household.Contains(Names[i]);
            return table;
        }
    }
}
