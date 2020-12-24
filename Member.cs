using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper.Configuration.Attributes;

namespace ClubEmailer
{
    public class Member
    {
        public string Callsign { get; set; }
        public string Name { get; set; }
        public string EmailAddress { get; set; }
        public string Period { get; set; }
        public bool GDPRConsent { get; set; }
    }
}
