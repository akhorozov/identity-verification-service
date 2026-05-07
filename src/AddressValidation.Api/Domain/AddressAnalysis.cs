namespace AddressValidation.Api.Domain;

/// <summary>
/// Represents USPS Delivery Point Validation (DPV) analysis and footnotes for a validated address.
/// This analysis indicates whether the address is deliverable and provides additional details.
/// All properties reflect data from Smarty US Street API's Metadata section.
/// </summary>
public class AddressAnalysis
{
    /// <summary>
    /// DPV Match Code indicating deliverability.
    /// 
    /// Values:
    /// - "Y" = Valid address (matches USPS database)
    /// - "D" = Valid CMRA (Commercial Mail Receiving Agency)
    /// - "N" = Invalid address (not found in USPS database)
    /// - "S" = Valid single number address (e.g., single-family rural route)
    /// - "" = Not checked/no match
    /// </summary>
    public string? DpvMatchCode { get; set; }

    /// <summary>
    /// DPV Footnotes providing reason codes for non-matches.
    /// Two-letter codes indicating specific delivery point conditions.
    /// Example: "AABB" where each character represents a specific footnote code.
    /// See USPS DPV documentation for complete interpretation.
    /// </summary>
    public string? DpvFootnotes { get; set; }

    /// <summary>
    /// Commercial Mail Receiving Agency (CMRA) indicator.
    /// "Y" if address is a mailbox rental location (e.g., UPS Store, PO Box service).
    /// "N" or empty if not a CMRA.
    /// </summary>
    public string? DpvCmra { get; set; }

    /// <summary>
    /// Vacant address indicator from USPS database.
    /// "Y" if USPS database indicates the address is vacant.
    /// "N" or empty if occupied or unknown.
    /// </summary>
    public string? DpvVacant { get; set; }

    /// <summary>
    /// Active delivery indicator.
    /// "Y" if the address is currently active and receiving mail.
    /// "N" or empty if inactive or unknown.
    /// </summary>
    public string? Active { get; set; }

    /// <summary>
    /// CASS standardization footnotes (two-letter codes).
    /// Provides additional context about address standardization.
    /// Examples: "AA" (address standardized), "AB" (ZIP+4 added), "AC" (ZIP+4 corrected).
    /// See CASS documentation for complete code reference.
    /// </summary>
    public string? Footnotes { get; set; }

    /// <summary>
    /// LACS Link Code (Locatable Address Conversion System).
    /// Indicates if a rural route address has been converted to city-delivery format.
    /// "00" = not converted; other codes indicate conversion type.
    /// </summary>
    public string? LacsLinkCode { get; set; }

    /// <summary>
    /// LACS Link Indicator.
    /// "Y" if the address was converted via LACS Link (rural to city conversion).
    /// "N" or empty if not converted.
    /// </summary>
    public string? LacsLinkIndicator { get; set; }

    /// <summary>
    /// Suite Link Match indicator.
    /// "Y" if the address matched via Suite Link (apartment/unit number correction).
    /// "N" or false if not corrected via Suite Link.
    /// </summary>
    public bool SuiteLinkMatch { get; set; }

    /// <summary>
    /// Residential Delivery Indicator (RDI).
    /// Indicates the nature/classification of the address.
    /// Values: "Residential", "Commercial", "Unknown"
    /// </summary>
    public string? ResidentialDeliveryIndicator { get; set; }

    /// <summary>
    /// Enhanced Line of Travel (eLOT) number for mail carrier sequencing.
    /// Provides USPS sequencing information indicating the order in which a mail carrier delivers to addresses on a route.
    /// </summary>
    public string? EnhancedLineOfTravel { get; set; }

    /// <summary>
    /// eLOT (Enhanced Line of Travel) Ascending indicator.
    /// "Y" if the ascending sequence for this address is in ascending order.
    /// "N" or empty otherwise.
    /// </summary>
    public string? EnhancedLineOfTravelAscending { get; set; }

    /// <summary>
    /// PO Box indicator.
    /// "Y" if this address is a PO Box.
    /// "N" or empty if not a PO Box.
    /// </summary>
    public string? PoBoxIndicator { get; set; }

    /// <summary>
    /// Federal Delivery indicator.
    /// "Y" if this is a federal (USPS) address.
    /// "N" or empty if not federal.
    /// </summary>
    public string? FederalDeliveryIndicator { get; set; }
}
