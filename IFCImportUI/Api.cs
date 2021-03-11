using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace IFCImportUI
{
    class Api
    {
        public static int Run( ref string server, ref string activities, ref string ifcPath, ref string wexbimPath )
        {
            int status = -1;

            try
            {
                string projectCode = Path.GetFileNameWithoutExtension(ifcPath);
                string wexbimJson = "";
                if (wexbimPath != null && !wexbimPath.Equals(""))
                {
                    wexbimJson = ",\"WexbimPath\":\"" + wexbimPath.Replace("\\", "\\\\") + "\"";
                }
                byte[] bytes = Encoding.UTF8.GetBytes(
                    "{\"command\":\"createProject\",\"project\":{\"Version\":1,\"Code\":\"" + 
                    projectCode + "\"" + wexbimJson + "}, \"activities\":" + activities+ "}"
                );

                // DEBUG
                /*
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"../../dest.txt"))
                {
                    file.WriteLine(
                        "{\"command\":\"createProject\",\"project\":{\"Version\":1,\"Code\":\"" +
                        projectCode + "\"" + wexbimJson + "}, \"activities\":" + activities + "}"
                    );
                }
                */
                // ~DEBUG

                WebRequest request = WebRequest.Create(server);
                request.Method = "POST";

                request.ContentLength = bytes.Length;
                Stream dataStream = request.GetRequestStream();
                dataStream.Write(bytes, 0, bytes.Length);
                dataStream.Close();

                WebResponse response = request.GetResponse();
                using (dataStream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(dataStream);
                    string responseFromServer = reader.ReadToEnd();
                    status = 0;
                }
                response.Close();
            }
            catch(Exception e)
            {
                ;
            }
            return status;
        }
    }
}
