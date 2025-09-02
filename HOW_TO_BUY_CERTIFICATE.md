# How to Purchase an IRS-Compliant X.509 Certificate

This guide provides instructions for purchasing an X.509 certificate that meets the requirements for the IRS Affordable Care Act (ACA) Information Returns (AIR) Application-to-Application (A2A) service.

These instructions are based on the IRS Publication 5165, Section 4.2.4.

---

### Step 1: Choose a Certificate Authority (CA)

The IRS only accepts digital certificates from specific, approved vendors. As of the latest IRS documentation, you can choose from the following:

*   **ORC ECA**
*   **Identrust** (for IGC certificates)

### Step 2: Purchase the Correct Certificate Type

It is critical to purchase the exact type of certificate required for the A2A service.

#### If using Identrust (Recommended):

1.  Navigate to the **Identrust Government Agencies** website: https://www.identrust.com/digital-certificates/federal-state-and-local-agencies
2.  Click the option to **"Buy Now"**.
3.  You will see a list of government programs. Please choose:
    *   `Department of Treasury - IRS Secure Data Transfer`
4.  When prompted for the certificate type, please choose:
    *   `IGC Standard Medium Assurance | Organization Identity | Device`

#### If using ORC ECA:

1.  Navigate to the **ORC ECA** website.
2.  Select the option to **"Order Component/Server Certificates"**.
3.  On the order screen, choose **"Server Certificates"**.

### Step 3: Critical - Verify Key Usage During Purchase

During the purchase and enrollment process, you may be asked about the certificate's intended use. You **must** ensure the certificate's **Key Usage** attribute is configured to include **BOTH** of the following values:

*   `digitalSignature`
*   `keyEncipherment`

If the certificate does not have both of these key usages, the IRS will reject it.

### Step 4: Download the Certificate

After completing the purchase, the Certificate Authority will provide you with the certificate file. This is typically a file with a `.pfx` or `.p12` extension, and it will be protected by a password that you create.

This `.pfx` file and its password are what we will need to complete and test the application.
