using System;
using System.Collections.Generic;
using System.Text;
using Nuke.Common.ProjectModel;

namespace Helpers
{
    public class ProjectDefinition
    {
        public string Name { get; set; }
        public Project Project { get; set; }
        public string Exe { get; set; }
        public string Dir { get; set; }
        public string DstExe { get; set; }
    }
}
