using Assimp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hyper
{
    public static class AssimpTest
    {
        public static Scene LoadObj(string path)
        {
            var context = new AssimpContext();
            Scene model = context.ImportFile(path, PostProcessSteps.Triangulate);

            return model;
        }
    }
}
