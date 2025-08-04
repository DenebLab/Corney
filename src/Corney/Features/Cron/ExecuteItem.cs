namespace Corney.Core.Features.Cron.Models
{
    public class ExecuteItem
    {
        public string Program { get; set; }
        public string Arguments { get; set; }
        public string[] ArgumentsArray { get; set; } = new string[0];
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Convert Arguments string to array if ArgumentsArray is not set
        /// </summary>
        public string[] GetArgumentsArray()
        {
            if (ArgumentsArray?.Length > 0)
                return ArgumentsArray;
                
            if (string.IsNullOrEmpty(Arguments))
                return new string[0];
                
            return Arguments.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        }
    }
}