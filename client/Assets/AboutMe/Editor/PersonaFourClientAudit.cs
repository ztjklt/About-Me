using UnityEditor;

namespace AboutMe.Editor
{
    public static class PersonaFourClientAudit
    {
        [MenuItem("About Me/Test four Persona client assets")]
        public static void Run() => PersonaFourRuntimeAudit.Run();
    }
}
