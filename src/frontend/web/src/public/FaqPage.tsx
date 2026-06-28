import { useState } from "react";
import { FaqAccordion, type FaqItem } from "./FaqAccordion";

/** FAQ content by category, verbatim from the legacy FAQ page. */
const CATEGORIES: { key: string; label: string; items: FaqItem[] }[] = [
  {
    key: "requirements",
    label: "Requirements",
    items: [
      {
        q: "Who is eligible to apply for clinical rotations through RotationsPlus?",
        a: [
          "US Pre-med students (PMS)",
          "International Medical Students (IMS)",
          "International Medical Graduates (IMG)",
          "Unmatched American Medical Graduates (AMG)"
        ]
      },
      {
        q: "Do I first need to pass my USMLE Step 1, OET or MCAT?",
        a: "No, however there may be a very rare Preceptor who may require you to pass or have a minimum passing score. If so, you will see this under the Requirements section on the Preceptor page."
      },
      {
        q: "What is the minimum duration of a clinical rotation?",
        a: "4 weeks is the minimum duration since Preceptors need to have enough time for proper teaching and evaluation. Some preceptors may make an exception, and you will see this on the Preceptor page."
      },
      {
        q: "What are the required documents I need to apply?",
        a: "Each Preceptor will have their own set of unique requirements, including proof of identity, education status, immunizations, health insurance and liability insurance. You will find this under the Preceptor page."
      },
      {
        q: "When are my required documents due?",
        a: "We need all documents uploaded 14 days prior to your start date."
      },
      {
        q: "What happens if I miss my document deadline?",
        a: "Delayed documents pose administrative and logistical difficulties to us and your Preceptor. If you happen to miss your document deadline, you will incur a nominal fee and may have your start date pushed back."
      },
      {
        q: "Do I need Medical Professional Liability Insurance?",
        a: "Yes, you will find a link to our partnered insurance company under the Requirements section on the Preceptor page."
      }
    ]
  },
  {
    key: "expectations",
    label: "Expectations",
    items: [
      {
        q: "Is this a hands-on clinical rotation?",
        a: [
          "If you are an IMS, IMG or AMG, yes you are allowed to have supervised patient contact i.e. taking history & physical and non-invasive exams. You may not perform any activities that may be deemed as the practice of medicine.",
          "If you are a PMS, you will only be allowed to observe."
        ]
      },
      { q: "Are there specific start dates?", a: "You may start the rotation on any Monday of the month." },
      {
        q: "Will I be going into the hospital?",
        a: "Each Preceptor will provide a different clinical setting experience and you can find this information on their Preceptor page."
      },
      {
        q: "Can I access the EMR system?",
        a: "This will vary from preceptor to preceptor and is up to their comfort level with you."
      },
      {
        q: "Can I do core or elective rotations in order to fulfill my school's curriculum?",
        a: "Absolutely! Please upload a Dean's Letter outlining your school's requirements and permission and we will be happy to assist you."
      },
      {
        q: "What do I get at the end of each rotation?",
        a: "You will receive a Clinical Evaluation and a performance based Letter of Recommendation (LOR)."
      }
    ]
  },
  {
    key: "lor",
    label: "LOR",
    items: [
      {
        q: "Am I guaranteed a Letter of Recommendation (LOR)?",
        a: "LORs should always be performance based. If you show up on time, demonstrate good medical knowledge and respect for the staff, you should not have any problem earning one."
      },
      {
        q: "Is the Letter of Recommendation on the hospital letterhead?",
        a: "Majority of our Preceptors will provide it on hospital letterhead per your request."
      },
      {
        q: "Am I allowed to see the LOR?",
        a: "You can ask your Preceptor to upload it to your dashboard if they feel comfortable doing so. From a residency application standpoint, we recommend that you waive your right to see it."
      }
    ]
  },
  {
    key: "payment",
    label: "Payment",
    items: [
      {
        q: "What modes of payment do you accept?",
        a: "We are happy to accept Visa, Mastercard, AMEX, Discover and ACH Bank Transfer if you have a US bank account."
      },
      {
        q: "When do I have to make the payment?",
        a: [
          "We collect payment in full at the time of your purchase.",
          "We may collect only a 10% deposit if the Preceptor you choose requires an approval. Once they approve, you will need to finalize the remaining payment within 48 hours to show commitment to the Preceptor.",
          "A unique feature that sets us apart — we offer our clients to unlock a preceptor's name prior to their full rotation purchase for only $25!"
        ]
      },
      {
        q: "Am I eligible for a refund?",
        a: "We do not process refunds since we have limited positions. Should you run into a difficulty, please do not hesitate to contact us if you need additional support as we're here to help!"
      },
      {
        q: "Can I change my start date after I make payment?",
        a: "Yes, as long as the Preceptor has availability for those dates. You may incur a small fee for a schedule change."
      },
      {
        q: "Can I transfer my paid dollars to a friend?",
        a: "Yes, kindly contact us and we can help you with this process."
      }
    ]
  },
  {
    key: "housing",
    label: "Housing & Travel",
    items: [
      {
        q: "Can you help with this?",
        a: "We strongly recommend using RotatingRoom (rotatingroom.com) for your housing needs. For some specific clinical programs, the physician may provide a room for an additional fee."
      }
    ]
  },
  {
    key: "visa",
    label: "Visa",
    items: [
      { q: "Do you sponsor Visas?", a: "We are not an educational entity and cannot sponsor a Visa." },
      {
        q: "Can you provide support for a Visa interview?",
        a: "Yes, we can provide a Program Acceptance Letter (PAL) for your B1/B2 Visa interview at the Consulate."
      },
      {
        q: "What is the deposit I need to pay before my Visa gets secured?",
        a: "10% deposit with remaining 90% due upon Visa approval."
      },
      {
        q: "What happens if my Visa is rejected?",
        a: "We will refund the deposit after we receive proof of Visa rejection."
      }
    ]
  },
  {
    key: "preceptors",
    label: "Preceptors",
    items: [
      {
        q: "Is my Preceptor academically affiliated?",
        a: "Majority of our Preceptors are affiliated to Residency programs, ACGME teaching hospitals or Universities. Please visit the Preceptor's page for more information."
      },
      {
        q: "When do I find out who my Preceptor is?",
        a: "You can unlock a Preceptor's name by paying $25 before you purchase the entire rotation. If you don't want to pay $25, you can complete the rotation purchase and you'll find your Preceptor's name on your Dashboard."
      },
      {
        q: "Can I contact my Preceptor before the rotation begins?",
        a: "Please do not contact the Preceptor ahead of time! They are focused on patient care and calling them during clinic hours can cause inconvenience. We work hard on affiliating with Preceptors so we kindly ask that you do not reach out to them directly for questions or to set up your own rotation on the side."
      },
      {
        q: "Can I change my Preceptor after I make payment?",
        a: "You do have the option to change your preceptor, but will incur a fee for this since we already got prior approval for you and provided payment to the Preceptor."
      },
      {
        q: "I need a specific specialty in a specific city, but I don't see it when I search. Can you help with that?",
        a: "Yes, we can take a 10% refundable deposit and will find you a Preceptor within 5 business days or you'll get your money back. Please contact info@rotationsplus.org for a request."
      }
    ]
  },
  {
    key: "other",
    label: "Other",
    items: [
      {
        q: "Why RotationsPlus?",
        a: [
          "Harnessing automation & technology leads to lower pricing.",
          "12 Years of experience leads to better offerings and stronger customer service.",
          "Transparency leads to less headaches with planning for accommodation and travel.",
          "Overall, RotationsPlus offers a safer, smoother and stress-free clinical rotation experience!"
        ]
      }
    ]
  }
];

/** /faq — clone of the legacy FAQ page: a category selector + an accordion of the active category. */
export function FaqPage() {
  const [active, setActive] = useState(0);
  const category = CATEGORIES[active];

  return (
    <div className="faq-page">
      <section className="public-page-head">
        <h1>FAQ</h1>
        <p>All the information you need to support your medical journey — all in one place.</p>
      </section>

      <section className="section faq-page-body">
        <div className="faq-tabs" role="tablist" aria-label="FAQ categories">
          {CATEGORIES.map((c, i) => (
            <button
              key={c.key}
              id={`faq-tab-${c.key}`}
              role="tab"
              aria-selected={i === active}
              aria-controls="faq-panel"
              className={`faq-tab${i === active ? " active" : ""}`}
              onClick={() => setActive(i)}
            >
              {c.label}
            </button>
          ))}
        </div>
        <div id="faq-panel" role="tabpanel" aria-labelledby={`faq-tab-${category.key}`}>
          <FaqAccordion key={category.key} items={category.items} />
        </div>
      </section>
    </div>
  );
}
