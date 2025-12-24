namespace FoMed.Api.Models;

// Join table: Service (lab package) <-> LabTest (catalog)
public class ServiceLabTest
{
    public int ServiceId { get; set; }
    public int LabTestId { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public Service Service { get; set; } = default!;
    public LabTest LabTest { get; set; } = default!;
}
