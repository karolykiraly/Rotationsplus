import { AudienceFunnelPage, type AudienceFunnelContent } from "./AudienceFunnelPage";
import heroImg from "../assets/images/marketing/for_preceptors.webp";
import aboutImg from "../assets/images/marketing/about_rotations.webp";
import aboutMobileImg from "../assets/images/marketing/mobile_about_rotations.webp";
import bannerImg from "../assets/images/marketing/becoming_doctor.webp";
import onboard1 from "../assets/images/marketing/onboard1.webp";
import onboard2 from "../assets/images/marketing/onboard2.webp";
import onboard3 from "../assets/images/marketing/onboard3.webp";
import onboard4 from "../assets/images/marketing/onboard4.webp";
import onboard5 from "../assets/images/marketing/onboard5.webp";
import mobileOnboard1 from "../assets/images/marketing/mobile_onboard1.webp";
import mobileOnboard2 from "../assets/images/marketing/mobile_onboard2.webp";
import mobileOnboard3 from "../assets/images/marketing/mobile_onboard3.webp";
import mobileOnboard4 from "../assets/images/marketing/mobile_onboard4.webp";
import mobileOnboard5 from "../assets/images/marketing/mobile_onboard5.webp";

/** /for-preceptors — clone of the legacy ForPreceptors page (content verbatim). CTA → CIAM sign-up. */
const CONTENT: AudienceFunnelContent = {
  heroTitle: "Take the Next Step as a Clinical Preceptor",
  heroText:
    "Help a bright pre-med/med student achieve their career goals by becoming a Preceptor with us.",
  heroCta: "Onboard Today",
  heroImg,
  aboutTitle: "About RotationsPlus",
  aboutText:
    "We want to make setting up clinical rotations a smooth, seamless and stress-free process. Our goal is to match our clients with preceptors quickly and efficiently, while providing 100% transparency.",
  aboutImg,
  aboutMobileImg,
  benefitsTitle: "The Benefits of Joining Our Preceptor Network",
  benefitsText:
    "With our various features, we provide an unmatched clinical rotation experience mutually beneficial for our preceptors as well as applicants.",
  benefits: [
    "User Friendly Dashboard",
    "On Time Payments",
    "Vetted Applicants",
    "Bonuses for your staff",
    "Efficient Customer Service"
  ],
  bannerImg,
  processTitle: "The Process to Onboard",
  processLead: "Join our Preceptor Network today!",
  processCta: "Join Us Today",
  steps: [
    {
      title: "Create a Login",
      text: "Signing up is quick, secure and easy.",
      img: onboard1,
      mobileImg: mobileOnboard1
    },
    {
      title: "Onboard",
      text: "Complete your profile by providing details about your academic experience and rotation commitments.",
      img: onboard2,
      mobileImg: mobileOnboard2
    },
    {
      title: "Go Live On Our Platform",
      text: "You are entered into our Preceptor Network to be searched by students looking for clinical rotations.",
      img: onboard3,
      mobileImg: mobileOnboard3
    },
    {
      title: "Precept & Get Paid on Time",
      text: "You are compensated for your preceptorship, along with many other attractive benefits.",
      img: onboard4,
      mobileImg: mobileOnboard4
    },
    {
      title: "Evaluate & Refer Other Preceptors",
      text: "Complete your student's evaluation, and help our Preceptor Network grow!",
      img: onboard5,
      mobileImg: mobileOnboard5
    }
  ],
  faqs: [
    {
      q: "How do you screen your students & grads?",
      a: "Our applicants are required to upload their CV, up-to-date immunization records, proof of health insurance and proof of legal US status. We conduct background checks and require all applicants to take a HIPAA course."
    },
    {
      q: "How long does Onboarding take?",
      a: "Approximately 15-20 mins. The initial process will take only 5 minutes, and then you will schedule a 5-10 minute call so we can introduce ourselves and answer all questions and concerns. After the call the remaining process of completing your W-9 and contract should only take an additional 5 mins."
    },
    {
      q: "How much of an honorarium am I able to earn per week?",
      a: "You dictate the amount you would like to be paid. We can provide guidance on average rates so you can maximize the number of students who want to enroll with you."
    },
    {
      q: "How does your honorarium process work?",
      a: "We send a % of your honorarium payment via ACH (bank to bank) at the time of the applicant's purchase. The second payment owed gets transferred on the applicant's first day of their rotation. The remaining balance arrives upon completion and uploading of their clinical evaluation. Guaranteed on-time payments!"
    },
    {
      q: "Do RotationsPlus and your students/grads have proper insurance coverage?",
      a: "Yes, RotationsPlus is equipped with the right insurance in order to contract with clinics and hospitals for clinical placement. In addition we require all of our students/grads to secure & upload their $1M/$3M Medical Professional Liability insurance coverage."
    }
  ]
};

export function ForPreceptorsPage() {
  return <AudienceFunnelPage content={CONTENT} />;
}
