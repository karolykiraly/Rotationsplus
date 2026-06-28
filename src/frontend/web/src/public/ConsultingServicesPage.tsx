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

/** /consulting-services — clone of the legacy ForConsultants page (content verbatim). CTA → CIAM. */
const CONTENT: AudienceFunnelContent = {
  heroTitle: "Get the Support you Need from Top Tier Physician Consultants",
  heroText:
    "Whether you're in need of document editing or interview prep, our Physician Consultants are here to guide you.",
  heroCta: "Search Now",
  heroImg,
  aboutTitle: "Don't Settle for Outsourced Editors",
  aboutText:
    "At RotationsPlus, our Consultants are board certified physicians that are eager to help you with your needs. You can select from current Residents, Physicians or Faculty/Selection Committee Members depending on your budget and unique specialty.",
  aboutImg,
  aboutMobileImg,
  benefitsTitle: "Custom Support for your Specific Specialty",
  benefitsText:
    "Preparing for Internal Medicine is very different than preparing for General Surgery or OB-GYN. We focus on the details that give you the best chance to Match.",
  benefits: [
    "ERAS Application",
    "Personal Statement",
    "Medical Student Performance Evaluation (MSPE)",
    "Interview Preparation",
    "Consulting on Various Topics"
  ],
  bannerImg,
  processTitle: "The Process",
  processLead: "Get Started Today to Get One Step Closer to a Match!",
  processCta: "Join Us Today",
  steps: [
    {
      title: "Create a Login",
      text: "Signing up is quick, secure and easy.",
      img: onboard1,
      mobileImg: mobileOnboard1
    },
    {
      title: "Search & Filter",
      text: "Browse our updated inventory to find the right Physician Consultant to help you with your document or interview prep needs.",
      img: onboard2,
      mobileImg: mobileOnboard2
    },
    {
      title: "Purchase your Consultation",
      text: "On the Program Details page, you can select the service that works best for you, and then add to Cart.",
      img: onboard3,
      mobileImg: mobileOnboard3
    },
    {
      title: "Schedule your Consultation",
      text: "Your Physician Consultant will then contact you to setup a time and date that works best for the both of you.",
      img: onboard4,
      mobileImg: mobileOnboard4
    },
    {
      title: "Complete your Consultation",
      text: "Your Consultant will complete your intended service so you can confidently submit your ERAS application or get ready for that life-changing interview.",
      img: onboard5,
      mobileImg: mobileOnboard5
    }
  ],
  faqs: [
    {
      q: "How can I trust the individuals who are helping me?",
      a: "We ONLY collaborate with US based physicians who are either in residency, are practicing or are currently Faculty members. These individuals have gone through the process themselves and there's no better person to guide you."
    },
    {
      q: "How many revisions do I get?",
      a: "This will vary depending on the Consultant you choose. You will find this information along with all the details and pricing on each specific Program Details page."
    },
    {
      q: "How long does it take to finalize?",
      a: "We know you're in a hurry. Our goal is to always be transparent about processing time and each Consultant will let you know what their timeline looks like."
    },
    {
      q: "What if I'm not satisfied with the service?",
      a: "Contact us immediately so we can make it right!"
    },
    {
      q: "Can you write my entire Personal Statement for me?",
      a: "We recommend that you put together a basic outline so we understand your personality and what means most to you. Our Consultants can then fill in the blanks to make your PS stand out from the rest!"
    }
  ]
};

export function ConsultingServicesPage() {
  return <AudienceFunnelPage content={CONTENT} />;
}
