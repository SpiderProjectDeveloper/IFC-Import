using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace IFCImportUI
{
    class Api
    {
        public static int Run( ref string server, ref string activities, 
            ref string materials, ref string materialAssignments, 
            ref string ifcPath, ref string wexbimPath, ref string updateProjectPath )
        {
            int status = -1;

            try
            {
                string projectCode = Path.GetFileNameWithoutExtension(ifcPath);
                string wexbimJson = "";
                if (wexbimPath != null && !wexbimPath.Equals("")) {
                    wexbimJson = ",\"WexbimPath\":\"" + wexbimPath.Replace("\\", "\\\\") + "\"";
                }
                string updateProjectJson = "";
                if (updateProjectPath != null && !updateProjectPath.Equals("")) {
                    updateProjectJson = ",\"projectPath\":\"" + updateProjectPath.Replace("\\", "\\\\") + "\"";
                }
                string materialsJson = "";
                if( materials.Length > 0 ) {
                    materialsJson = ", \"materials\": " + materials;
                }
                string materialAssignmentsJson = "";
                if (materialAssignments.Length > 0) {
                    materialAssignmentsJson = ", \"activitiesMaterials\": " + materialAssignments;
                }
                byte[] bytes = Encoding.UTF8.GetBytes(
                    "{\"command\":\"createProject\",\"project\":{\"Version\":1,\"Code\":\"" +
                    projectCode + "\"" + wexbimJson + updateProjectJson + "}, \"activities\":" + activities +
                    materialsJson + materialAssignmentsJson + "}"
                );

                // DEBUG
                /*
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"..\..\dest.txt"))
                {
                    file.WriteLine(
                        "{\"command\":\"createProject\",\"project\":{\"Version\":1,\"Code\":\"" +
                        projectCode + "\"" + wexbimJson + "}, \"activities\":" + activities +
                        materialsJson + materialAssignmentsJson + "}"
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
