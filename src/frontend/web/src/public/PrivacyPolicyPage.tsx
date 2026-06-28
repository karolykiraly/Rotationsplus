/** The 14-section contents list shown on the legacy Privacy Policy page. */
const CONTENTS = [
  "What information do we collect?",
  "How do we use your information?",
  "Will your information be shared with anyone?",
  "Who will your information be shared with?",
  "Do we use cookies and other tracking technologies?",
  "How long do we keep your information?",
  "How do we keep your information safe?",
  "Do we collect information from minors?",
  "What are your privacy rights?",
  "Controls for do-not-track features",
  "Do California residents have specific privacy rights?",
  "Do we make updates to this notice?",
  "How can you contact us about this notice?",
  "How can you review, update or delete the data we collect from you?"
];

/** /privacy-policy — reproduction of the legacy Privacy Policy. NOTE: the live legacy page only renders
 *  the introduction, the 14-section contents list, and the body of section 1; the remaining sections'
 *  bodies are not present in the source. This faithfully mirrors what's live and should be completed
 *  with full legal copy before the public www cutover. */
export function PrivacyPolicyPage() {
  return (
    <div className="legal">
      <section className="public-page-head">
        <h1>Privacy Policy</h1>
        <p className="legal-updated">Last updated June 10, 2021</p>
      </section>

      <section className="section legal-body">
        <div className="legal-intro">
          <p>
            Thank you for choosing to be part of our community at RotationsPlus, LLC ("Company", "we",
            "us", "our"). We are committed to protecting your personal information and your right to
            privacy. If you have any questions or concerns about this privacy notice, or our practices
            with regards to your personal information, please contact us at{" "}
            <a className="link" href="mailto:info@rotationsplus.com">info@rotationsplus.com</a>.
          </p>
          <p>
            When you visit our website <b>https://www.rotationsplus.org</b> (the "Website"), and more
            generally, use any of our services (the "Services", which include the Website), we
            appreciate that you are trusting us with your personal information. We take your privacy
            very seriously. In this privacy notice, we seek to explain to you in the clearest way
            possible what information we collect, how we use it and what rights you have in relation to
            it. We hope you take some time to read through it carefully, as it is important. If there
            are any terms in this privacy notice that you do not agree with, please discontinue use of
            our Services immediately.
          </p>
          <p>
            This privacy notice applies to all information collected through our Services (which, as
            described above, includes our Website), as well as, any related services, sales, marketing
            or events.
          </p>
          <p>
            <b>
              Please read this privacy notice carefully as it will help you understand what we do with
              the information that we collect.
            </b>
          </p>
        </div>

        <nav className="legal-toc" aria-label="Contents">
          <ol>
            {CONTENTS.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ol>
        </nav>

        <section className="legal-section" id="what-we-collect">
          <h2>1. What Information Do We Collect?</h2>
          <p>
            <b>In Short:</b> We collect personal information that you provide to us.
          </p>
          <p>
            We collect personal information that you voluntarily provide to us when you register on the
            Website, express an interest in obtaining information about us or our products and Services,
            when you participate in activities on the Website or otherwise when you contact us.
          </p>
          <p>
            The personal information that we collect depends on the context of your interactions with
            us and the Website, the choices you make and the products and features you use. The
            personal information we collect may include the following:
          </p>
          <p>
            <b>Personal Information Provided by You.</b> We collect names; phone numbers; email
            addresses; mailing addresses; job titles; usernames; passwords; contact preferences;
            contact or authentication data; billing addresses; debit/credit card numbers; medical
            license number; gender; passport and/or driver's license; photos; health information;
            education information; academia & affiliation information; profession information; w-9 tax
            information; and other similar information.
          </p>
          <p>
            <b>Payment Data.</b> We may collect data necessary to process your payment if you make
            purchases, such as your payment instrument number (such as a credit card number), and the
            security code associated with your payment instrument. All payment data is stored by Stripe.
            You may find their privacy notice link(s) here:{" "}
            <a className="link" href="https://stripe.com/privacy" target="_blank" rel="noopener noreferrer">
              https://stripe.com/privacy
            </a>
            .
          </p>
        </section>
      </section>
    </div>
  );
}
