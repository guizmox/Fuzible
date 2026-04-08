using FuzibleFramework;
using System.Collections.Generic;

namespace Fuzible.Controleurs
{
    internal class CrossQuery_CTL
    {
        private readonly CONNStrings CSS;
        internal CrossQuery_CTL(string sUser)
        {
            CSS = new CONNStrings(sUser);
        }

        internal List<CONNString> GetConnections()
        {
            return CSS.CSList;
        }

        internal List<string> CheckConnStringUsage(string sConnID)
        {
            return CSS.CheckConnStringUsage(sConnID);
        }
    }
}
