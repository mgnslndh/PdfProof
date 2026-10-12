#set document(title: "Invoice", date: datetime(year: 2026, month: 1, day: 15))
#set text(size: 10pt, lang: "en")
#set page(paper: "a4", footer: context align(center)[Page #counter(page).display() of #counter(page).final().first()])

= Invoice

Questions? Visit #link("https://example.com/support")[our support site] or e-mail us.

#link(<terms>)[Read the terms and conditions]

#table(
  columns: 3,
  table.header[Item][Qty][Amount],
  [Consulting, senior], [10], [14 500,00 SEK],
)

#pagebreak()

== Terms and conditions <terms>

- Payment within 30 days.
