using CasCap.Models;
using System;
namespace CasCap.ViewModels
{
    public class flattened
    {
        public string id { get; set; }
        public string description { get; set; }
        //public string productUrl { get; set; }//ignore for the moment?
        //public string baseUrl { get; set; }//ignore for the moment?
        public string mimeType { get; set; }
        //public MediaMetaData mediaMetadata { get; set; }//flattened in properties below
        public string filename { get; set; }

        //public ContributorInfo? contributorInfo { get; set; }//ignore for the moment?

        public DateTime creationTime { get; set; }
        public string width { get; set; }
        public string height { get; set; }

        public float focalLength { get; set; }//photo
        public float apertureFNumber { get; set; }//photo
        public int isoEquivalent { get; set; }//photo
        public float exposureTime { get; set; }//photo

        public double fps { get; set; }//video
        public string status { get; set; }//video

        public string cameraMake { get; set; }//photo & video
        public string cameraModel { get; set; }//photo & video

        public string[] albumIds { get; set; }
        public GooglePhotosContentCategoryType[] contentCategoryTypes { get; set; }
    }
}