using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Devsense.PHP.Text;
using Devsense.PHP.Syntax;

namespace Phyl.CodeAnalysis
{
    public struct FileToken : IComparable<FileToken>, IEquatable<FileToken>
    {
        #region Constructors
        public FileToken(Tokens type, Span position, string text)
        {
            Type = type;
            Position = position;
            Text = text;
        }
        #endregion

        #region Overriden methods
        public override int GetHashCode()
        {
            return this.Position.GetHashCode();
        }
        #endregion

        #region Properties
        public Tokens Type { get; set; }
        public Span Position { get; set; }
        public string Text { get; set; }
        #endregion

        #region Methods
        public int CompareTo(FileToken t)
        {
            return Position.Start.CompareTo(t.Position.Start);
        }

        public bool Equals(FileToken t)
        {
            return this.Position.Start == t.Position.Start;
        }
        #endregion
    }
}
