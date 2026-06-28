import { LegalPage, type LegalContent } from "./LegalPage";

/** /terms — faithful reproduction of the legacy RotationsPlus Terms of Service. (Legal copy cloned
 *  from the live site; warrants a final legal proofread before the public www cutover.) */
const TERMS: LegalContent = {
  title: "RotationsPlus Terms of Service",
  updated: "Updated February 16th 2026",
  intro: [
    {
      text: 'This Client Agreement (the "Agreement") is entered into between RotationsPlus LLC, a California limited liability company ("RP") and the Student/Graduate ("Client") and is effective as of the date (such date, the "Effective Date") that Client clicks "I Agree" on the RP website located at www.rotationsplus.org/cart (the "Website").'
    },
    { text: "RECITALS" },
    { text: "Whereas, Client is a healthcare student or graduate;" },
    {
      text: 'Whereas, RP is a clinical placement company connecting its clients with U.S.-licensed MDs and DOs (collectively referred to as "Preceptors") at clinics and hospitals ("Programs") for the purpose of obtaining insured, hands-on clinical rotations, observerships, research and virtual experiences ("Rotations"), as well as document editing, interview preparation and one-on-one consulting ("Consultations") needed to fulfill its clients\' school\'s curriculum or to improve its clients\' chances of moving to the next step of their medical career; and'
    },
    {
      text: "Whereas, Client desires to engage RP to place Client with Preceptors, and RP agrees to provide such services pursuant to the terms set forth in this Agreement."
    }
  ],
  sections: [
    {
      id: "rp-services",
      title: "RP Services",
      blocks: [
        {
          text: 'RP agrees to use commercially reasonable efforts to place Client with affiliated Preceptors by providing Client with access to the Website and providing Client with consulting services and affiliates in connection with such placement efforts (collectively, the "Services"). Client acknowledges that RP is not an educational entity.'
        }
      ]
    },
    {
      id: "client-obligations",
      title: "Client Obligations",
      blocks: [
        { text: "During the term of this Agreement, Client agrees to do the following:" },
        {
          clauses: [
            { label: "a.", text: "Submit and confirm a valid email address in order to create a login to the Website;" },
            { label: "b.", text: "Submit valid personal and educational details in order to search the database of available Rotations;" },
            {
              label: "c.",
              text: "Authorize RP to conduct a check of Client's criminal and personal history, which may include without limitation:",
              sub: [
                "Determining whether Client has had any misdemeanors within the last 10 years or any felony convictions;",
                "Authorizing RP and its affiliates and/or service providers to investigate Client's background including criminal, credit, employment and educational history, irrespective of whether the background check is mandated by the applicable jurisdiction. This may include acquiring consumer reports from appropriate agencies. Client agrees to advise RP of any changes to Client's criminal record at any time. Client consents to a random drug test before, during and up to 30 days after completion of any Rotations. International Clients may be asked to provide an official police report from Client's home country;",
                "Acknowledging that if Client has been involved in a DUI, RP will not provide enrollment if there was injury in connection with such DUI, or if Client was on their way to or from a medical job or educational responsibility when the DUI occurred; and",
                "Acknowledging that any information discovered through a background search after payment has been processed will result in immediate termination of this Agreement, and that Client will not be eligible for any refund."
              ]
            },
            { label: "d.", text: "Access the client dashboard on the Website to upload all immunization records and requirements needed by the Preceptor and/or Rotation Site (hospital, clinic, nursing home, university);" },
            { label: "e.", text: "Read and comply with the List of Requirements found on the Website under each unique Program ID's page, plus any additional requirements that may arise after the Client's enrollment;" },
            { label: "f.", text: "Immediately inform RP if Client has any infectious disease or mental health problem that may result in injury or liability to a patient or Preceptor at the Rotation Site;" },
            { label: "g.", text: "Watch a video and take a quiz for Health Insurance Portability and Accountability Act (HIPAA) Certification or any additional certifications required by the Preceptor and/or Rotation Site;" },
            { label: "h.", text: "Secure accommodations and travel to and from the Rotation Site;" },
            { label: "i.", text: "Secure a valid U.S Visa, if required;" },
            { label: "j.", text: "Refrain from contacting the Preceptor, or to visit the Rotation Site, in advance of Client's confirmed Rotation start date;" },
            { label: "k.", text: 'Refrain from performing any invasive procedures unless Client is a student who is completing the Rotation as a part of their medical school\'s curriculum. "Invasive procedure" shall mean any procedure, test, therapy or surgery that involves puncturing the skin or muscle or into a vein or artery, such as starting an intravenous line, performing cardiac catheterization, injections, inserting central lines, or any other procedure that may pose a risk to patient;' },
            { label: "l.", text: "Not to engage in hands-on medical care for any US pre-med students, or for any other Clients if the Preceptor, Rotation Site or state jurisdiction prohibits hands-on patient contact;" },
            { label: "m.", text: "Never to engage in the practice of medicine with or without the use of medication (such as behavioral therapy, or providing a diagnosis, prognosis or interpreting lab data), as defined by relevant state and federal regulatory agencies;" },
            { label: "n.", text: "Refrain from representing himself/herself as a physician, or from using any title, degree or credential initials, or any other designation that could be construed as defining Client's role as that of a physician;" },
            { label: "o.", text: "Refrain from accepting compensation for Client's activities during the Rotation;" },
            { label: "p.", text: "Refrain from engaging in any activity prohibited by relevant state and federal regulatory agencies;" },
            { label: "q.", text: "Promptly report any criminal activities by the Preceptor to the proper authorities;" },
            { label: "r.", text: "Contact RP immediately if dismissed by the Preceptor or Rotation Site and in such event to not return to the Rotation Site or contact the Preceptor as this may result in legal action by the Rotation Site or Preceptor;" },
            { label: "s.", text: "Refrain from staying with the Preceptor past the end date of the Rotation;" },
            { label: "t.", text: "Refrain from setting up Client's own Rotation with any RP Preceptor by contacting them directly or indirectly for at least 12 months after the end of the Rotation;" },
            { label: "u.", text: "Refrain from sharing the name or details of a Preceptor with other associates without prior written consent from RP;" },
            { label: "v.", text: "Refrain from accepting payments or gifts, services, favors, or consideration of any kind, or any sort of remuneration to or from Preceptor, the Rotation Site or anyone affiliated to either;" },
            { label: "w.", text: "Adhere to and comply with all HIPAA requirements by not sharing any confidential patient information, unless as allowed by law;" },
            { label: "x.", text: "During a virtual telemedicine Rotation, Client shall find a quiet location without any distractions from children, pets or potential loud noises;" },
            { label: "y.", text: "Remain in good financial standing. If custom payment plans are set up and Client defaults, the Rotation will be put on hold;" },
            {
              label: "z.",
              text: "Evaluate the Preceptor's performance at the end of each Rotation. Client's Preceptor Evaluation (PE) data will be used on the Website, but will not mention the Client's name. In order for Client to move to the next Rotation, Client must complete a Preceptor Evaluation through their dashboard before the start of their next Rotation.",
              sub: [
                "If a Preceptor creates a hostile workplace, defined as unwelcome or offensive behavior in the workplace, which causes Client to feel uncomfortable, scared, or intimidated in their Rotation Site, Client must inform RP immediately so the Rotation Site can determine a course of action. RP will first provide Client with other Preceptor options. As a last resort it can provide partial to full refunds or issuance of a credit. If a Preceptor is charged with a crime, the Client can reschedule with another Preceptor for a $199 Fee.",
                "Adhere to a professional medical dress codes as determined by Preceptor and/or the Rotation Site;",
                "Refrain from using alcohol or drugs while servicing patients or at the Rotation Site; and",
                "Refrain from using their mobile devices during the Rotation."
              ]
            }
          ]
        }
      ]
    },
    {
      id: "fees",
      title: "Fees",
      blocks: [
        {
          clauses: [
            {
              label: "a. Rotation Fee.",
              text: 'Client agrees to pay in full the fee applicable for any available Rotation chosen by Client through the Website (such fee, the "Rotation Fee"). Client will pay the Rotation Fee as follows:',
              sub: [
                'A refundable Preceptor Approval Deposit ("PAD") equaling 10% of the total Rotation Fee for any Preceptor that requires a rotation approval ("RA"), upon RP\'s receipt of the Preceptor\'s RA. RP will provide a full refund of the PAD after 5 business days if it cannot secure a Preceptor that matches the Client\'s requirements. If RP is able to secure the RA within the 5 business days, the student is not eligible for a refund for the PAD. The student is only allowed to change the Preceptor or the proposed rotation date one time after the RA.',
                'If the Preceptor does not require RA for each client request and is instead marked as "Instant Availability", Client shall make full payment immediately upon choosing such Preceptor\'s Rotation.'
              ]
            },
            { label: "b. Consulting Fee.", text: "Client agrees to pay in full for any Consultation services through the Website. If the Client does not submit their initial documents on time to the Consultant, RP is not liable for any delays past the expected turnaround time listed on the Program ID page. Client is only allowed to have the maximum number of revisions as mentioned on the Program ID page. Any revision above the number mentioned will incur an additional fee, which will also be clearly defined on the Program ID page. The Consultant is not obligated to share their mobile number, LinkedIn page, or any other information besides email address with the Client. Any changes to an approved appointment by the Consultant will incur a $99 fee." },
            { label: "c. Preceptor Unlocking Fee.", text: 'By default, Client may unlock only one Preceptor\'s full name through a search on the Website before remitting the Preceptor Approval Deposit ("PAD"). However, Client may pay to RP an additional one-time fee of $99 to unlock a Preceptor\'s full name. Client agrees to not contact the Preceptor directly after obtaining this information or will incur a $1000 fine for violating this term. This fee is non-refundable.' },
            { label: "d. Late Fee.", text: "Client agrees to complete all applicable pre-Rotation requirements set forth in Section 2 by no later than Document Deadline mentioned, which ranges from 7-35 days prior to the start date of Client's Rotation. If Client fails to do so within 48-96 hours of the deadline, Client will incur a $199-$250 late fee, which shall be immediately due and payable to RP. Failure to pay will result in the Client not being able to start on time and potentially forfeiting the rotation and fees originally paid." },
            { label: "e. Change Fee.", text: "If payment and outstanding requirements are not submitted 7 days before the start of the Rotation, the start date may be pushed back by 7 days. If the Preceptor cannot honor the new change in start date, Client must select another Rotation Site and pay the difference in the fees, plus a $250 change fee. Any lower-priced Rotation Site will not qualify for a refund, but can be kept as a credit towards future purchases for up to 24 months. Credits can be tracked on the Website's Client Dashboard. A second change will incur up to a $500 fee, depending on the specific Rotation Site. For any Program that requires a hospital application a specific number of weeks in advance of the start date, the Client agrees that they will only be allowed to make a change one week prior to the number of weeks needed by the hospital. This allows RP enough time to enroll another Client. No changes will be allowed after this deadline." },
            { label: "f. Visa Fee.", text: "If Client cannot obtain a valid visa, RP will provide a full refund of the required deposit upon proof of visa rejection, minus a 5% fee for any credit card processing fee. If proof cannot be provided, the funds will transfer to an account credit to be used within 24 months. If the Client does not have a Visa and wishes to enroll in a Clinical Site with a Hospitalist, i.e. 100% inpatient, the 10% deposit will not be refunded if the Visa is denied, and cannot be used as credit." },
            { label: "g. No Refunds.", text: "There shall be no refunds of any fees paid to RP under this Agreement besides a Visa refund, or an issue with border crossing into the U.S.. However, case-by-case partial refunds may be considered for medical emergencies for the Client or an immediate family member (brother, sister, mother, father, child, spouse). Proof of medical emergency must be documented. Partial refunds may take up to 30 days for processing and will incur up to a 10% merchant processing fee." },
            { label: "h. Delay by Preceptor or Rotation Site.", text: "If a delay or cancellation occurs because of the Preceptor or Rotation Site, RP will immediately provide Client with other Preceptor options and will provide a $100-$500 credit towards future Rotations or towards the full cost of any services or later fee, which shall expire in 24 months. The credit shall be $100 if before 30 days, $250 if within 14 days, and $500 if within 7 days. RP is not required to provide a refund due to a cancellation caused by a Rotation Site due to an infectious disease, natural disaster, or travel ban. RP will not be responsible for any accommodation or travel expenses incurred by Client as result of a delay or cancellation by a Preceptor or Rotation Site." }
          ]
        }
      ]
    },
    {
      id: "liability",
      title: "Acknowledgment of Liability",
      blocks: [
        {
          text: "Client acknowledges and agrees that Client is solely liable for the outcome of any and all actions that may result from any activity which is outside the scope of this Agreement. Furthermore, Client acknowledges that neither RP nor its insurance providers or insurance agents or affiliated Preceptors have any obligation to provide medical liability coverage for any portion of the Rotation. Client shall be solely responsible for any and all malpractice suits and liabilities arising out of Client's participation in such Rotations. If while enrolled with RP, Client engages in any activities that can be construed as the practice of medicine by relevant state and federal regulatory agencies, or gives health related advice or interacts with patients without direct supervision of a Preceptor, this Agreement will be immediately terminated. Client acknowledges that practicing medicine without a license is strictly prohibited, and may be punishable by criminal and civil law. RP is not liable for any damages or liability as a result of Client participating in an act or omission that constitutes a prohibited activity."
        }
      ]
    },
    {
      id: "pal",
      title: "Program Acceptance Letter",
      blocks: [
        {
          clauses: [
            { label: "a.", text: "This Letter can be provided after the Client has made a 10% deposit and the Preceptor has approved the request. The Client must upload Proof of Education and Proof of Identity before being allowed to download the PAL." },
            {
              label: "b.",
              text: "The PAL will outline the following",
              sub: [
                "Client's Full name",
                "Client's Address",
                "Rotation(s) Start Date/End Date",
                "Specialty",
                "Rotation Location",
                "Preceptor/RS Name",
                "Goals/Objectives"
              ]
            },
            { label: "c.", text: "On RP letterhead and cannot guarantee that the Preceptor or RS can provide it on their letterhead;" },
            { label: "d.", text: "No legal Visa advice or guarantee of Visa approval are given;" },
            { label: "e.", text: "PAL can only be provided after payment and all requirements have been met (minus proof of Visa) and will be able to be downloaded from the Client Dashboard" },
            { label: "f.", text: "RP cannot legally sponsor Visas as we are not an educational entity with SEVIS accreditation;" },
            { label: "g.", text: "Fee of $50-$75 per Program Acceptance Letter per Clinical Site. This fee is non-refundable;" },
            {
              label: "h.",
              text: "Once a Visa is approved by the Client, the Client must finalize payment within 5 business days or RotationsPlus has the right to:",
              sub: [
                "Keep the deposit which will be forfeited by the Client, and/or",
                "Contact the Embassy to let them know about the cancellation, and/or",
                "Charge the remaining balance owed by the Client with the credit card originally used via Stripe Credit Card processing. The client will be required to complete this Credit Card Authorization form.",
                "RotationsPlus may extend this payment deadline, but only with written agreement before the initial 10% deposit has been made."
              ]
            },
            { label: "i.", text: "Certain Programs may offer Hospital Invite Letters and those deposit amounts and letter fees will be clearly mentioned under the Description section on that specific Program Details page. RotationsPlus cannot expedite these letters and the fees are non-refundable." },
            { label: "j.", text: "Client can switch to another Program once Visa gets secured, but there will be a $99 Transfer Fee to the new program." }
          ]
        }
      ]
    },
    {
      id: "lor",
      title: "Letters of Recommendation",
      blocks: [
        {
          text: "Letters of recommendation from Preceptors are always performance-based and cannot be guaranteed. The Preceptor does not have any obligation to share this with the Client. If the Preceptor does decide to share it, the Client will receive a notification and it will be made available on Client's dashboard. Letters of recommendation may be on clinic, hospital or other letterhead. RP is not obliged to provide a refund if the letterhead information changes."
        }
      ]
    },
    {
      id: "guarantees",
      title: "Disclaimer of Guarantees",
      blocks: [
        {
          text: "RP cannot guarantee hospital exposure or experience for Client unless RP has a contract with the hospital. RP does not guarantee a residency Interview or a residency match position through NRMP"
        }
      ]
    },
    {
      id: "preceptor-info",
      title: "Preceptor Information",
      blocks: [
        {
          text: "Preceptor information, including affiliated hospitals, in-office procedures conducted, weekly schedule, pricing and any data found on the Preceptor's page is subject to change without notice as it is affected by supply & demand and other factors not in control of RP as Preceptors are individual contractors and not employees of RP."
        }
      ]
    },
    {
      id: "subcontracted",
      title: "Sub-contracted Programs",
      blocks: [
        {
          text: "RotationsPlus offers two types of contracts. A Direct Contract and a Sub-Contract. A Direct Contract means that RotationsPlus has direct access to the Preceptor and can provide customer service through the entire process. A Sub-contract means that RotationsPlus has an agreement to sell and market another company's rotations, research or consulting programs, but has to go through an external coordinator and therefore does not have direct access to the Preceptor. RotationsPlus adds a fee to the price of the sub-contracted rotation for time spent for marketing, enrollment and technical support. Therefore the fee may be higher than going directly to the sub-contracted organization."
        },
        {
          clauses: [
            { label: "a.", text: "RotationsPlus is not responsible for any delays or physician changes that are caused by the sub-contracted organization" },
            { label: "b.", text: "Any customer services issues related to the clinical experience must go through the sub-contracted organization's coordinator as RotationsPlus does not have direct access to their preceptor or hospital coordinator" },
            { label: "c.", text: "RotationsPlus agrees to follow up with the coordinators of the sub-contracted organization if there is a lack of response more than 10 business days from their coordinator." },
            { label: "d.", text: "RotationsPlus will not provide any refunds due to customer service issues caused by the sub-contracted organization" },
            { label: "e.", text: "RotationsPlus is not responsible for any customer service issues with the housing provided by sub-contracted organizations" },
            { label: "f.", text: "Any ratings/reviews left online for RotationsPlus should reflect the communication and support from RotationsPlus' staff and not the sub-contracted organization's staff." }
          ]
        }
      ]
    },
    {
      id: "promotions",
      title: "Promotions",
      blocks: [
        {
          text: 'RotationsPlus may, in its sole discretion, offer promotional discounts, coupon codes, credits, referral incentives, or other promotional offers (collectively, "Promotions"). Promotions may provide a percentage discount (e.g., "10% off") or a fixed-amount discount (e.g., "$200 off"), and may be subject to additional terms disclosed at the time of the Promotion ("Promotion Terms"). Unless otherwise required by law or expressly stated in the applicable Promotion Terms:'
        },
        {
          clauses: [
            { label: "a. Eligibility and Limits.", text: "Promotions may be limited to eligible users with an active legal U.S. non-immigrant or immigrant Visa status, accounts, locations, Services, specialties, dates, payment methods, minimum purchase amounts, or other conditions. Promotions may be limited to one per person/account, may not be combinable with other offers, and may be void where prohibited" },
            { label: "b. No Cash Value; Non-Transferable.", text: "Promotions have no cash value, are not redeemable for cash (except where required by law), are non-transferable, may not be resold, and may not be substituted" },
            { label: "c. How to Apply.", text: "Promotions must be applied at the time of checkout or purchase (as applicable). Promotions cannot be applied retroactively to prior purchases, invoices, or completed transactions" },
            { label: "d. Exclusions.", text: "Unless expressly stated in the Promotion Terms, Promotions do not apply to taxes, third-party fees, bank/wire fees, or other charges that are not the RotationsPlus Service price" },
            { label: "e. Changes; Termination; Errors.", text: "RotationsPlus may modify, suspend, or terminate any Promotion at any time. RotationsPlus may correct errors, inaccuracies, or omissions (including typographical, pricing, or system errors) related to Promotions and may cancel, modify, or refuse transactions impacted by such errors to the extent permitted by law" },
            { label: "f. Misuse.", text: "RotationsPlus may refuse redemption, reverse a Promotion, cancel an order, or adjust pricing if it reasonably believes the Promotion is being used in violation of these Terms, the Promotion Terms, or applicable law, including suspected fraud, abuse, or circumvention" },
            { label: "g. Refunds and Cancellations.", text: "If a Promotion is applied to a purchase that is later refunded or canceled (in whole or in part), any refund will be limited to the amount actually paid by you (net of the Promotion discount actually applied), unless otherwise required by law or expressly stated in the Promotion Terms. For percentage-off Promotions, the discount will be allocated proportionally to the discounted items/services. For fixed-amount Promotions, the discount may be allocated across eligible items/services in RotationsPlus's reasonable discretion. Any portion of a Promotion that was used will not be refunded or reissued unless required by law or expressly stated in the Promotion Terms." }
          ]
        }
      ]
    },
    {
      id: "contact",
      title: "Contact",
      blocks: [
        {
          text: "If you have any questions about this Terms of Service, please contact us immediately at info@rotationsplus.com or call +1.657.214.7174."
        }
      ]
    }
  ]
};

export function TermsPage() {
  return <LegalPage content={TERMS} />;
}
