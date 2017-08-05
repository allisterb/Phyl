using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Phyl.CodeAnalysis.Graphs
{
    public class ControlFlowGraphEdgeTag
    {
        [XmlAttribute]
        public int Id { get; set; }
    }
}
