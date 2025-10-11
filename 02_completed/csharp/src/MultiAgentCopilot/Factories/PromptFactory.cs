namespace MultiAgentCopilot.MultiAgentCopilot.Factories
{
    public static class PromptFactory
    {
    
        public static string Termination(string topic)
        {

            var terminationPrompt = $"{File.ReadAllText("Prompts/TerminationStrategy.prompty")}";
            return terminationPrompt.Replace("{topic}", topic);
        }


        public static string Selection(string discussion, string particpants)
        {
            var selectionPrompt = $"{File.ReadAllText("Prompts/SelectionStrategy.prompty")}";
            return selectionPrompt.Replace("{participants}", particpants).Replace("{discussion}", discussion);
        }

        public static string Filter(string topic)
        {
            var filterPrompt = $"{File.ReadAllText("Prompts/FilterStrategy.prompty")}";
            return filterPrompt.Replace("{topic}", topic);

        }

    }
}
