namespace WebLab.Models;

public class GlampingActivity
{
    public int GlampingSiteId { get; set; }
    public GlampingSite? GlampingSite { get; set; }

    public int ActivityId { get; set; }
    public Activity? Activity { get; set; }
}
