using CsvHelper.Configuration;

namespace ClubEmailer
{
    public class MemberMap : ClassMap<Member>
    {
        public MemberMap()
        {
            Map(m => m.GDPRConsent)
                .Name("GDPR Consent")
                .TypeConverterOption.BooleanValues(true, true, "Yes", "Y")
                .TypeConverterOption.BooleanValues(false, true, "No", "N", "N/A", "");
            
            Map(m => m.Period).Name("Period");
            Map(m => m.Name).Name("Name");
            Map(m => m.EmailAddress).Name("Email Address");
        }
    }
}
