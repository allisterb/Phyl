using System;
using System.Xml;

using QuickGraph;

namespace Phyl.QuickGraph.Serialization
{
    public abstract class SerializerBase<TVertex,TEdge>
        where TEdge :IEdge<TVertex>
    {
        public bool EmitDocumentDeclaration {get;set;}
    }
}
